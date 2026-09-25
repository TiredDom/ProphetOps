using System.Buffers.Binary;
using System.Security.Cryptography;

namespace ProphetOps.Api;

public sealed record BackupEncryptionSettings(bool Enabled, byte[]? Key, string? KeyId)
{
    public const string Algorithm = "AES-256-GCM";
    public const string None = "none-local-test-only";
    public const string EnvelopeExtension = ".prophetops-backup.pobak";
    public const string ZipExtension = ".prophetops-backup.zip";

    public static BackupEncryptionSettings FromConfiguration(IConfiguration configuration)
    {
        var configured = configuration["Backup:Encryption:Key"];
        if (string.IsNullOrWhiteSpace(configured))
            return new BackupEncryptionSettings(false, null, null);

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
        return new BackupEncryptionSettings(true, key, string.IsNullOrWhiteSpace(keyId) ? "operator-supplied" : keyId.Trim());
    }
}

public static class BackupEncryptedEnvelope
{
    private static readonly byte[] Magic = "POBAK"u8.ToArray();
    private const byte Version = 1;
    private const int NonceLength = 12;
    private const int TagLength = 16;

    public static async Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, CancellationToken cancellationToken)
    {
        var plaintext = await File.ReadAllBytesAsync(plaintextPath, cancellationToken);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        using (var aes = new AesGcm(key, TagLength))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        await using var output = File.Create(encryptedPath);
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

    public static async Task DecryptFile(string encryptedPath, string plaintextPath, byte[] key, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(encryptedPath, cancellationToken);
        var plaintext = Decrypt(bytes, key);
        await File.WriteAllBytesAsync(plaintextPath, plaintext, cancellationToken);
    }

    public static byte[] Decrypt(byte[] envelope, byte[] key)
    {
        if (envelope.Length < Magic.Length + 1 + 1 + 1 + NonceLength + TagLength + 8)
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

        var plaintext = new byte[ciphertextLength];
        using var aes = new AesGcm(key, TagLength);
        aes.Decrypt(nonce, envelope.AsSpan(offset).ToArray(), tag, plaintext);
        return plaintext;
    }
}
