using Microsoft.Data.Sqlite;

namespace ProphetOps.Api;

public sealed class StoragePaths
{
    private StoragePaths(string root)
    {
        Root = root;
        DatabasePath = UnderRoot("prophetops.db");
        UploadsPath = UnderRoot("uploads");
        KeysPath = UnderRoot("keys");
        BackupStagingPath = UnderRoot("backups");
        PackageImagesPath = UnderRoot("uploads", "packages");
    }

    public string Root { get; }
    public string DatabasePath { get; }
    public string UploadsPath { get; }
    public string KeysPath { get; }
    public string BackupStagingPath { get; }
    public string PackageImagesPath { get; }
    public string DatabaseConnectionString => new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();

    public static StoragePaths FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var hosted = HostedRuntime.IsEnabled(configuration);
        var configuredRoot = configuration["Storage:Root"];
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            if (hosted || !configuration.GetValue("Storage:AllowContentRootFallback", false))
                throw new InvalidOperationException("Storage:Root must be configured to an absolute durable directory.");
            configuredRoot = environment.ContentRootPath;
        }

        if (!Path.IsPathFullyQualified(configuredRoot))
            throw new InvalidOperationException("Storage:Root must be an absolute path.");

        var root = Path.GetFullPath(configuredRoot);
        if (File.Exists(root))
            throw new InvalidOperationException("Storage:Root must be a directory, not a file.");

        var paths = new StoragePaths(root);
        paths.EnsureWritableDirectories();
        return paths;
    }

    public string UploadedPackageImage(string storedName)
    {
        var fileName = Path.GetFileName(storedName);
        if (!string.Equals(fileName, storedName, StringComparison.Ordinal))
            throw new InvalidOperationException("Stored image names must not contain path segments.");
        return UnderRoot("uploads", "packages", fileName);
    }

    private string UnderRoot(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(new[] { Root }.Concat(segments).ToArray()));
        var root = Root.EndsWith(Path.DirectorySeparatorChar) ? Root : Root + Path.DirectorySeparatorChar;
        if (!path.Equals(Root, StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage paths must stay under Storage:Root.");
        return path;
    }

    private void EnsureWritableDirectories()
    {
        foreach (var path in new[] { Root, UploadsPath, PackageImagesPath, KeysPath, BackupStagingPath, Path.GetDirectoryName(DatabasePath)! })
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
    }
}

public static class HostedRuntime
{
    public static bool IsEnabled(IConfiguration configuration)
    {
        var configured = configuration.GetValue<bool?>("Hosted:Enabled");
        if (configured.HasValue) return configured.Value;
        return string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
    }
}
