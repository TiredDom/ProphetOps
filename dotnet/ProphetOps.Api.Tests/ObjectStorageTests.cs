using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class ObjectStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "prophetops-objects-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("packages/../escape.png")]
    [InlineData("/absolute.png")]
    [InlineData("packages\\escape.png")]
    public async Task Local_object_storage_rejects_keys_that_escape_the_private_root(string key)
    {
        var storage = new LocalObjectStorage(StoragePaths.FromConfiguration(Config(), Env()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.PutAsync(key, new MemoryStream([1, 2, 3]), "image/png", CancellationToken.None));
    }

    [Fact]
    public async Task Local_object_storage_round_trips_content_type_length_and_owned_stream()
    {
        var storage = new LocalObjectStorage(StoragePaths.FromConfiguration(Config(), Env()));
        await storage.PutAsync("packages/photo.png", new MemoryStream([1, 2, 3]), "image/png", CancellationToken.None);

        var stored = await storage.OpenReadAsync("packages/photo.png", CancellationToken.None);

        Assert.NotNull(stored);
        await using (stored)
        {
            Assert.Equal("image/png", stored.ContentType);
            Assert.Equal(3, stored.Length);
            using var copy = new MemoryStream();
            await stored.Content.CopyToAsync(copy);
            Assert.Equal([1, 2, 3], copy.ToArray());
        }
    }

    [Fact]
    public async Task Local_object_storage_delete_surfaces_io_failures()
    {
        var storage = new LocalObjectStorage(
            StoragePaths.FromConfiguration(Config(), Env()),
            _ => throw new IOException("synthetic delete failure"));
        await storage.PutAsync("packages/photo.png", new MemoryStream([1]), "image/png", CancellationToken.None);

        var error = await Assert.ThrowsAsync<IOException>(() =>
            storage.DeleteAsync("packages/photo.png", CancellationToken.None));

        Assert.Contains("synthetic delete failure", error.Message);
    }

    [Fact]
    public void Supabase_s3_put_request_uses_private_object_key_and_r2_compatible_stream_flags()
    {
        using var body = new MemoryStream([1, 2, 3]);

        var request = SupabaseS3ObjectStorage.CreatePutRequest("bucket", "prefix/packages/photo.png", body, "image/png", 3);

        Assert.Equal("bucket", request.BucketName);
        Assert.Equal("prefix/packages/photo.png", request.Key);
        Assert.Equal("image/png", request.ContentType);
        Assert.Equal(3, request.Headers.ContentLength);
        Assert.False(request.AutoCloseStream);
        Assert.True(request.DisablePayloadSigning);
        Assert.True(request.DisableDefaultChecksumValidation);
        Assert.Equal(S3CannedACL.Private, request.CannedACL);
    }

    [Fact]
    public void Supabase_s3_client_config_keeps_custom_endpoint_when_region_is_configured()
    {
        var options = new SupabaseS3ObjectStorageOptions(
            new Uri("https://project.supabase.co/storage/v1/s3"),
            "package-images",
            "ap-southeast-1",
            "access-key",
            "secret-key",
            "agency/private/",
            true,
            30);

        var config = AwsSupabaseS3ObjectClient.CreateConfig(options);

        Assert.Equal("https://project.supabase.co/storage/v1/s3", config.ServiceURL);
        Assert.Equal("ap-southeast-1", config.AuthenticationRegion);
        Assert.True(config.ForcePathStyle);
    }

    [Fact]
    public void Backup_s3_client_config_keeps_custom_endpoint_when_region_is_configured()
    {
        var options = new S3BackupStorageOptions(
            new Uri("https://project.supabase.co/storage/v1/s3"),
            "backups",
            "ap-southeast-1",
            "access-key",
            "secret-key",
            "backups/",
            true,
            30);

        var config = AwsS3BackupClient.CreateConfig(options);

        Assert.Equal("https://project.supabase.co/storage/v1/s3", config.ServiceURL);
        Assert.Equal("ap-southeast-1", config.AuthenticationRegion);
        Assert.True(config.ForcePathStyle);
    }

    private IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Root"] = _root,
            ["Business:TimeZone"] = "Asia/Manila",
        })
        .Build();

    private IWebHostEnvironment Env() => new TestEnvironment(_root);

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ProphetOps.Api.Tests";
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
