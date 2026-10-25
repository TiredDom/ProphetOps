using System.IO.Compression;

namespace ProphetOps.Api;

public interface IBackupPackageFileOperations
{
    void CreateZip(string sourceDirectory, string destination);
    void CreateZip(string sourceDirectory, string destination, long maxTotalBytes, long currentBaseBytes) =>
        CreateZip(sourceDirectory, destination);

    Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, CancellationToken cancellationToken);
    Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, long maxTotalBytes, long currentBaseBytes, CancellationToken cancellationToken) =>
        EncryptFile(plaintextPath, encryptedPath, key, cancellationToken);
    Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, long maxTotalBytes, long currentBaseBytes, long maxInputBytes, CancellationToken cancellationToken) =>
        EncryptFile(plaintextPath, encryptedPath, key, maxTotalBytes, currentBaseBytes, cancellationToken);
}

public sealed class BackupPackageFileOperations : IBackupPackageFileOperations
{
    public void CreateZip(string sourceDirectory, string destination) =>
        CreateZip(sourceDirectory, destination, long.MaxValue, 0);

    public void CreateZip(string sourceDirectory, string destination, long maxTotalBytes, long currentBaseBytes)
    {
        using var fileStream = File.Create(destination);
        using var quotaStream = new QuotaBoundedWriteStream(fileStream, currentBaseBytes, maxTotalBytes, "zip packaging");
        using (var archive = new ZipArchive(quotaStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            var sourceDirInfo = new DirectoryInfo(sourceDirectory);
            foreach (var fileInfo in sourceDirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, fileInfo.FullName).Replace('\\', '/');
                var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var sourceStream = fileInfo.OpenRead();
                sourceStream.CopyTo(entryStream);
            }
        }
    }

    public Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, CancellationToken cancellationToken) =>
        EncryptFile(plaintextPath, encryptedPath, key, long.MaxValue, 0, BackupEncryptionSettings.DefaultMaxInputBytes, cancellationToken);

    public Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, long maxTotalBytes, long currentBaseBytes, CancellationToken cancellationToken) =>
        EncryptFile(plaintextPath, encryptedPath, key, maxTotalBytes, currentBaseBytes, BackupEncryptionSettings.DefaultMaxInputBytes, cancellationToken);

    public Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, long maxTotalBytes, long currentBaseBytes, long maxInputBytes, CancellationToken cancellationToken) =>
        BackupEncryptedEnvelope.EncryptFile(plaintextPath, encryptedPath, key, maxTotalBytes, currentBaseBytes, maxInputBytes, cancellationToken);
}
