using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace ProphetOps.Api;

public sealed record BackupEncryptionSettings(
    bool Enabled,
    byte[]? Key,
    string? KeyId,
    long MaxInputBytes = BackupEncryptionSettings.DefaultMaxInputBytes)
{
    public const string Algorithm = "AES-256-GCM";
    public const string None = "none-local-test-only";
    public const string EnvelopeExtension = ".prophetops-backup.pobak";
    public const string ZipExtension = ".prophetops-backup.zip";
    public const long DefaultMaxInputBytes = 32L * 1024 * 1024; // 32 MiB conservative default for 512 MB host

    public static BackupEncryptionSettings FromConfiguration(IConfiguration configuration)
    {
        var maxInputBytes = DefaultMaxInputBytes;
        var configuredMaxInput = configuration["Backup:Encryption:MaxInputBytes"];
        if (!string.IsNullOrWhiteSpace(configuredMaxInput))
        {
            if (!long.TryParse(configuredMaxInput, out var parsedMax) || parsedMax <= 0)
            {
                throw new InvalidOperationException("Backup:Encryption:MaxInputBytes must be a positive number of bytes.");
            }
            maxInputBytes = parsedMax;
        }

        var configured = configuration["Backup:Encryption:Key"];
        if (string.IsNullOrWhiteSpace(configured))
            return new BackupEncryptionSettings(false, null, null, maxInputBytes);

        byte[] key;
        try
        {
            key = Convert.FromBase64String(configured);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Backup:Encryption:Key must be a base64-encoded 32-byte key.", ex);
        }

        if (key.Length != 32)
            throw new InvalidOperationException("Backup:Encryption:Key must decode to exactly 32 bytes for AES-256-GCM.");

        var keyId = configuration["Backup:Encryption:KeyId"];
        return new BackupEncryptionSettings(true, key, string.IsNullOrWhiteSpace(keyId) ? "operator-supplied" : keyId.Trim(), maxInputBytes);
    }
}

public static class BackupEncryptedEnvelope
{
    private static readonly byte[] Magic = "POBAK"u8.ToArray();
    private const byte Version = 1;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    public const int HeaderLength = 5 + 1 + 1 + 1 + NonceLength + TagLength + 8; // 44 bytes

    public static Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, CancellationToken cancellationToken) =>
        EncryptFile(plaintextPath, encryptedPath, key, long.MaxValue, 0, BackupEncryptionSettings.DefaultMaxInputBytes, cancellationToken);

    public static Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, long maxTotalBytes, long currentBaseBytes, CancellationToken cancellationToken) =>
        EncryptFile(plaintextPath, encryptedPath, key, maxTotalBytes, currentBaseBytes, BackupEncryptionSettings.DefaultMaxInputBytes, cancellationToken);

    public static async Task EncryptFile(
        string plaintextPath,
        string encryptedPath,
        byte[] key,
        long maxTotalBytes,
        long currentBaseBytes,
        long maxInputBytes,
        CancellationToken cancellationToken)
    {
        if (maxInputBytes <= 0)
            throw new InvalidOperationException("Backup:Encryption:MaxInputBytes must be a positive number of bytes.");

        var plaintextInfo = new FileInfo(plaintextPath);
        if (!plaintextInfo.Exists)
            throw new FileNotFoundException($"Plaintext file not found for encryption: '{plaintextPath}'");

        // Enforce in-memory encryption input size BEFORE ReadAllBytesAsync or allocating ciphertext
        if (plaintextInfo.Length > maxInputBytes)
        {
            throw new InvalidOperationException(
                $"Backup encryption input size ({plaintextInfo.Length} bytes) exceeded the configured maximum in-memory encryption limit of {maxInputBytes} bytes. Increase Backup:Encryption:MaxInputBytes or reduce backup size.");
        }

        var expectedEnvelopeLength = HeaderLength + plaintextInfo.Length;
        if (currentBaseBytes + expectedEnvelopeLength > maxTotalBytes)
        {
            throw new InvalidOperationException(
                $"Backup staging hard quota exceeded before envelope encryption: projected staging size ({currentBaseBytes + expectedEnvelopeLength} bytes) exceeded configured limit of {maxTotalBytes} bytes.");
        }

        var plaintext = await File.ReadAllBytesAsync(plaintextPath, cancellationToken);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        using (var aes = new AesGcm(key, TagLength))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        await using var fileStream = File.Create(encryptedPath);
        await using var output = new QuotaBoundedWriteStream(fileStream, currentBaseBytes, maxTotalBytes, "envelope encryption");

        await output.WriteAsync(Magic, cancellationToken);
        output.WriteByte(Version);
        output.WriteByte((byte)NonceLength);
        output.WriteByte((byte)TagLength);
        await output.WriteAsync(nonce, cancellationToken);
        await output.WriteAsync(tag, cancellationToken);
        var length = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(length, ciphertext.Length);
        await output.WriteAsync(length, cancellationToken);
        await output.WriteAsync(ciphertext, cancellationToken);
    }

    public static Task DecryptFile(string encryptedPath, string plaintextPath, byte[] key, CancellationToken cancellationToken) =>
        DecryptFile(encryptedPath, plaintextPath, key, BackupEncryptionSettings.DefaultMaxInputBytes, cancellationToken);

    public static async Task DecryptFile(
        string encryptedPath,
        string plaintextPath,
        byte[] key,
        long maxPlaintextBytes,
        CancellationToken cancellationToken)
    {
        if (maxPlaintextBytes <= 0)
            throw new InvalidOperationException("Backup:Encryption:MaxInputBytes must be a positive number of bytes.");

        var encryptedInfo = new FileInfo(encryptedPath);
        if (!encryptedInfo.Exists)
            throw new FileNotFoundException($"Encrypted envelope file not found for decryption: '{encryptedPath}'");

        var maxEnvelopeBytes = maxPlaintextBytes + HeaderLength;
        if (encryptedInfo.Length > maxEnvelopeBytes)
        {
            throw new InvalidOperationException(
                $"Encrypted backup envelope size ({encryptedInfo.Length} bytes) exceeded the maximum allowed decryption limit of {maxEnvelopeBytes} bytes. Increase Backup:Encryption:MaxInputBytes or verify envelope authenticity.");
        }

        var bytes = await File.ReadAllBytesAsync(encryptedPath, cancellationToken);
        var plaintext = Decrypt(bytes, key, maxPlaintextBytes);
        await File.WriteAllBytesAsync(plaintextPath, plaintext, cancellationToken);
    }

    public static byte[] Decrypt(byte[] envelope, byte[] key, long maxPlaintextBytes = BackupEncryptionSettings.DefaultMaxInputBytes)
    {
        if (maxPlaintextBytes <= 0)
            throw new InvalidOperationException("Backup:Encryption:MaxInputBytes must be a positive number of bytes.");

        if (envelope.Length < HeaderLength)
            throw new InvalidOperationException("Encrypted backup envelope is malformed.");
        if (!envelope.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidOperationException("Encrypted backup envelope has an unsupported magic value.");

        var offset = Magic.Length;
        var version = envelope[offset++];
        if (version != Version) throw new InvalidOperationException("Encrypted backup envelope has an unsupported version.");
        var nonceLength = envelope[offset++];
        var tagLength = envelope[offset++];
        if (nonceLength != NonceLength || tagLength != TagLength)
            throw new InvalidOperationException("Encrypted backup envelope has unsupported parameters.");

        var nonce = envelope.AsSpan(offset, nonceLength).ToArray();
        offset += nonceLength;
        var tag = envelope.AsSpan(offset, tagLength).ToArray();
        offset += tagLength;
        var ciphertextLength = BinaryPrimitives.ReadInt64BigEndian(envelope.AsSpan(offset, 8));
        offset += 8;
        if (ciphertextLength < 0 || ciphertextLength != envelope.Length - offset)
            throw new InvalidOperationException("Encrypted backup envelope length is invalid.");

        if (ciphertextLength > maxPlaintextBytes)
        {
            throw new InvalidOperationException(
                $"Encrypted backup envelope payload ({ciphertextLength} bytes) exceeded the maximum allowed decryption limit of {maxPlaintextBytes} bytes.");
        }

        var plaintext = new byte[ciphertextLength];
        using var aes = new AesGcm(key, TagLength);
        aes.Decrypt(nonce, envelope.AsSpan(offset).ToArray(), tag, plaintext);
        return plaintext;
    }
}
