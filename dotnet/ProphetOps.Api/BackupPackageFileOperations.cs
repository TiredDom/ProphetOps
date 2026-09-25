using System.IO.Compression;

namespace ProphetOps.Api;

public interface IBackupPackageFileOperations
{
    void CreateZip(string sourceDirectory, string destination);
    Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, CancellationToken cancellationToken);
}

public sealed class BackupPackageFileOperations : IBackupPackageFileOperations
{
    public void CreateZip(string sourceDirectory, string destination) =>
        ZipFile.CreateFromDirectory(sourceDirectory, destination, CompressionLevel.Optimal, includeBaseDirectory: false);

    public Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, CancellationToken cancellationToken) =>
        BackupEncryptedEnvelope.EncryptFile(plaintextPath, encryptedPath, key, cancellationToken);
}
