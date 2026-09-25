using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ProphetOps.Api;
using Xunit;

namespace ProphetOps.Api.Tests;

public class StoragePathsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "prophetops-storage-" + Guid.NewGuid());

    [Fact]
    public void Hosted_mode_requires_explicit_absolute_root()
    {
        var config = Config(("Hosted:Enabled", "true"));
        var error = Assert.Throws<InvalidOperationException>(() => StoragePaths.FromConfiguration(config, Env(_directory)));
        Assert.Contains("Storage:Root", error.Message);
    }

    [Fact]
    public void Missing_root_does_not_fall_back_to_content_root_unless_legacy_compatibility_is_explicit()
    {
        var config = Config();
        var error = Assert.Throws<InvalidOperationException>(() => StoragePaths.FromConfiguration(config, Env(_directory)));
        Assert.Contains("Storage:Root", error.Message);
    }

    [Fact]
    public void Explicit_legacy_content_root_fallback_is_narrow_and_visible()
    {
        var config = Config(("Storage:AllowContentRootFallback", "true"));
        var paths = StoragePaths.FromConfiguration(config, Env(_directory));

        Assert.Equal(Path.GetFullPath(_directory), paths.Root);
        Assert.Equal(Path.Combine(Path.GetFullPath(_directory), "prophetops.db"), paths.DatabasePath);
    }

    [Fact]
    public void Root_must_be_absolute()
    {
        var config = Config(("Storage:Root", "relative-storage-root"));
        var error = Assert.Throws<InvalidOperationException>(() => StoragePaths.FromConfiguration(config, Env(_directory)));
        Assert.Contains("absolute", error.Message);
    }

    [Fact]
    public void Root_must_be_a_directory()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "not-a-directory");
        File.WriteAllText(file, "");
        var config = Config(("Storage:Root", file));

        var error = Assert.Throws<InvalidOperationException>(() => StoragePaths.FromConfiguration(config, Env(_directory)));
        Assert.Contains("directory", error.Message);
    }

    [Fact]
    public void Resolved_paths_are_under_root_and_writable()
    {
        var root = Path.Combine(_directory, "storage");
        var paths = StoragePaths.FromConfiguration(Config(("Storage:Root", root)), Env(_directory));

        Assert.All(new[] { paths.DatabasePath, paths.UploadsPath, paths.KeysPath, paths.BackupStagingPath, paths.PackageImagesPath },
            path => Assert.StartsWith(Path.GetFullPath(root), path, StringComparison.OrdinalIgnoreCase));
        Assert.True(Directory.Exists(paths.UploadsPath));
        Assert.True(Directory.Exists(paths.KeysPath));
        Assert.True(Directory.Exists(paths.BackupStagingPath));
        Assert.True(Directory.Exists(paths.PackageImagesPath));
    }

    [Theory]
    [InlineData("../x.png")]
    [InlineData("folder/x.png")]
    public void Stored_image_names_cannot_escape_package_uploads(string storedName)
    {
        var paths = StoragePaths.FromConfiguration(Config(("Storage:Root", Path.Combine(_directory, "storage"))), Env(_directory));
        Assert.Throws<InvalidOperationException>(() => paths.UploadedPackageImage(storedName));
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();

    private static IHostEnvironment Env(string contentRoot) => new TestEnvironment { ContentRootPath = contentRoot };

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ProphetOps.Api.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
