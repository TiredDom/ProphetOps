using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class H2aImageLifecycleTests : IDisposable
{
    private readonly ImageFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Authenticated_image_route_streams_private_object_and_disposes_the_object_stream()
    {
        _factory.Objects.Store("packages/private.png", Png(), "image/png");
        await SetPackageImage("PKG-101", "packages/private.png");

        using var client = await AuthenticatedClient.Login(_factory);
        var response = await client.GetAsync("/api/inventory/PKG-101/image");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png(), await response.Content.ReadAsByteArrayAsync());
        Assert.True(_factory.Objects.LastOpenedStreamDisposed);
    }

    [Fact]
    public async Task Image_route_uses_safe_content_type_from_stored_key_not_object_metadata()
    {
        _factory.Objects.Store("packages/private.png", Png(), "text/html");
        await SetPackageImage("PKG-101", "packages/private.png");

        using var client = await AuthenticatedClient.Login(_factory);
        var response = await client.GetAsync("/api/inventory/PKG-101/image");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Private_image_route_rejects_unauthenticated_and_forbidden_users()
    {
        _factory.Objects.Store("packages/private.png", Png(), "image/png");
        await SetPackageImage("PKG-101", "packages/private.png");
        await AddUser("blocked@prophetops.local", "blocked123", "ReportsOnly");

        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/inventory/PKG-101/image")).StatusCode);

        using var forbidden = await AuthenticatedClient.Login(_factory, "blocked@prophetops.local", "blocked123");
        Assert.Equal(HttpStatusCode.Forbidden, (await forbidden.GetAsync("/api/inventory/PKG-101/image")).StatusCode);
    }

    [Fact]
    public async Task Referenced_missing_object_returns_not_found_without_exposing_storage_details()
    {
        await SetPackageImage("PKG-101", "packages/missing.png");

        using var client = await AuthenticatedClient.Login(_factory);
        var response = await client.GetAsync("/api/inventory/PKG-101/image");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("packages/missing.png", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Replacing_and_deleting_images_retains_old_bytes_and_records_retryable_cleanup()
    {
        using var client = await AuthenticatedClient.Login(_factory);

        var first = await Upload(client, revision: 1);
        var firstDto = first.RootElement;
        var firstKey = firstDto.GetProperty("imageUrl").GetString()!.Split("v=")[1];

        var second = await Upload(client, firstDto.GetProperty("revision").GetInt32());
        var secondDto = second.RootElement;
        var secondKey = secondDto.GetProperty("imageUrl").GetString()!.Split("v=")[1];

        Assert.NotEqual(firstKey, secondKey);
        Assert.True(_factory.Objects.Contains(firstKey));
        Assert.True(_factory.Objects.Contains(secondKey));
        Assert.Empty(_factory.Objects.DeletedKeys);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Contains(await db.ObjectCleanupEntries.AsNoTracking().ToListAsync(),
                entry => entry.ObjectKey == firstKey && entry.Reason == ObjectCleanupReasons.PackageImageReplaced);
        }

        var deleted = await DeleteImage(client, "PKG-101", secondDto.GetProperty("revision").GetInt32());
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(_factory.Objects.Contains(secondKey));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Contains(await db.ObjectCleanupEntries.AsNoTracking().ToListAsync(),
                entry => entry.ObjectKey == secondKey && entry.Reason == ObjectCleanupReasons.PackageImageDeleted);
        }
    }

    [Fact]
    public async Task Interrupted_upload_does_not_change_database_or_audit_trail()
    {
        using var client = await AuthenticatedClient.Login(_factory);
        _factory.Objects.FailNextPut = true;

        var response = await client.PostAsync("/api/inventory/PKG-101/image",
            FileNamed("photo.png", "image/png", Png(), revision: 1));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = await db.TravelPackages.SingleAsync(p => p.Code == "PKG-101");
        Assert.Null(package.ImagePath);
        Assert.DoesNotContain(await db.AuditEntries.ToListAsync(),
            entry => entry.EntityType == "TravelPackage" && entry.EntityCode == "PKG-101" && entry.Summary!.Contains("Photo"));
    }

    [Fact]
    public async Task Database_rollback_after_object_creation_records_retryable_orphan_cleanup_without_immediate_delete()
    {
        await using var factory = new FailingImageCommitFactory();
        using var client = await AuthenticatedClient.Login(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PostAsync("/api/inventory/PKG-101/image",
            FileNamed("photo.png", "image/png", Png(), revision: 1)));

        var orphanKey = Assert.Single(factory.Objects.PutKeys);
        Assert.True(factory.Objects.Contains(orphanKey));
        Assert.Empty(factory.Objects.DeletedKeys);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = await db.TravelPackages.SingleAsync(p => p.Code == "PKG-101");
        Assert.Null(package.ImagePath);
        Assert.Contains(await db.ObjectCleanupEntries.AsNoTracking().ToListAsync(),
            entry => entry.ObjectKey == orphanKey && entry.Reason == ObjectCleanupReasons.PackageImageUploadRolledBack);
    }

    [Fact]
    public async Task Unknown_save_outcome_retains_uploaded_object_for_reference_recheck()
    {
        await using var factory = new UnknownImageCommitFactory();
        using var client = await AuthenticatedClient.Login(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PostAsync("/api/inventory/PKG-101/image",
            FileNamed("photo.png", "image/png", Png(), revision: 1)));

        var key = Assert.Single(factory.Objects.PutKeys);
        Assert.True(factory.Objects.Contains(key));
        Assert.Empty(factory.Objects.DeletedKeys);
    }

    private async Task SetPackageImage(string code, string key)
    {
        _ = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = await db.TravelPackages.SingleAsync(p => p.Code == code);
        package.ImagePath = key;
        await db.SaveChangesAsync();
    }

    private async Task AddUser(string email, string password, string role)
    {
        _ = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(new User
        {
            Name = "Blocked",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            Status = "Active",
        });
        await db.SaveChangesAsync();
    }

    private static async Task<JsonDocument> Upload(HttpClient client, int revision)
    {
        var response = await client.PostAsync("/api/inventory/PKG-101/image",
            FileNamed("photo.png", "image/png", Png(), revision));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
    }

    private static MultipartFormDataContent FileNamed(string name, string contentType, byte[] content, int revision)
    {
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent
        {
            { part, "file", name },
            { new StringContent(revision.ToString()), "revision" },
        };
    }

    private static Task<HttpResponseMessage> DeleteImage(HttpClient client, string code, int revision)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/inventory/{code}/image")
        {
            Content = JsonContent.Create(new { revision }),
        };
        return client.SendAsync(request);
    }

    private static byte[] Png()
    {
        var bytes = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }

    private sealed class ImageFactory : ApiFactory
    {
        public RecordingObjectStorage Objects { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.Remove(services.Single(d => d.ServiceType == typeof(IObjectStorage)));
                services.AddSingleton<IObjectStorage>(Objects);
            });
        }
    }

    private class FailingImageCommitFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "prophetops-rollback-factory-" + Guid.NewGuid().ToString("N"));

        public FailingImageCommitFactory()
        {
            Directory.CreateDirectory(_storageRoot);
            _connection.Open();
        }

        public RecordingObjectStorage Objects { get; } = new();

        protected virtual SaveChangesInterceptor Interceptor { get; } = new ThrowOnPackageImageSave();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = "true",
                    ["Storage:Root"] = _storageRoot,
                    ["Business:TimeZone"] = "Asia/Manila",
                }));
            builder.ConfigureServices(services =>
            {
                var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions)).ToList();
                foreach (var d in toRemove) services.Remove(d);

                services.AddDbContext<AppDbContext>(options => options
                    .UseSqlite(_connection)
                    .AddInterceptors(Interceptor));
                services.Remove(services.Single(d => d.ServiceType == typeof(IObjectStorage)));
                services.AddSingleton<IObjectStorage>(Objects);
            });
        }

        public new async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _connection.DisposeAsync();
            if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private sealed class ThrowOnPackageImageSave : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            ThrowIfPackageImageChanged(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfPackageImageChanged(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void ThrowIfPackageImageChanged(DbContext? db)
        {
            if (db?.ChangeTracker.Entries<TravelPackage>().Any(entry =>
                    entry.State == EntityState.Modified && entry.Property(package => package.ImagePath).IsModified) == true)
                throw new InvalidOperationException("Synthetic image commit failure.");
        }
    }

    private sealed class UnknownImageCommitFactory : FailingImageCommitFactory
    {
        protected override SaveChangesInterceptor Interceptor { get; } = new ThrowAfterPackageImageSave();
    }

    private sealed class ThrowAfterPackageImageSave : SaveChangesInterceptor
    {
        private bool _packageImageChanged;

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            _packageImageChanged = PackageImageChanged(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _packageImageChanged = PackageImageChanged(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            ThrowIfPackageImageChanged();
            return base.SavedChanges(eventData, result);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfPackageImageChanged();
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        private void ThrowIfPackageImageChanged()
        {
            if (_packageImageChanged)
                throw new InvalidOperationException("Synthetic unknown image commit outcome.");
        }

        private static bool PackageImageChanged(DbContext? db) =>
            db?.ChangeTracker.Entries<TravelPackage>().Any(entry =>
                entry.State == EntityState.Modified && entry.Property(package => package.ImagePath).IsModified) == true;
    }
}

public sealed class RecordingObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, (byte[] Bytes, string ContentType)> _objects = new(StringComparer.Ordinal);

    public bool FailNextPut { get; set; }
    public bool FailNextDelete { get; set; }
    public bool LastOpenedStreamDisposed { get; private set; }
    public List<string> DeletedKeys { get; } = [];
    public List<string> PutKeys { get; } = [];

    public bool Contains(string key) => _objects.ContainsKey(key);

    public void Store(string key, byte[] bytes, string contentType) =>
        _objects[key] = (bytes, contentType);

    public async Task PutAsync(string key, Stream body, string contentType, CancellationToken cancellationToken)
    {
        if (FailNextPut)
        {
            FailNextPut = false;
            throw new ObjectStorageUnavailable("Synthetic upload interruption.");
        }

        using var copy = new MemoryStream();
        await body.CopyToAsync(copy, cancellationToken);
        _objects[key] = (copy.ToArray(), contentType);
        PutKeys.Add(key);
    }

    public async Task<bool> PutIfNotExistsAsync(string key, Stream body, string contentType, CancellationToken cancellationToken)
    {
        if (FailNextPut)
        {
            FailNextPut = false;
            throw new ObjectStorageUnavailable("Synthetic upload interruption.");
        }

        if (_objects.ContainsKey(key))
        {
            return false;
        }

        using var copy = new MemoryStream();
        await body.CopyToAsync(copy, cancellationToken);
        _objects[key] = (copy.ToArray(), contentType);
        PutKeys.Add(key);
        return true;
    }

    public Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        LastOpenedStreamDisposed = false;
        if (!_objects.TryGetValue(key, out var item)) return Task.FromResult<StoredObject?>(null);
        var stream = new ObservedMemoryStream(item.Bytes, () => LastOpenedStreamDisposed = true);
        return Task.FromResult<StoredObject?>(new StoredObject(stream, item.ContentType, item.Bytes.Length));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (FailNextDelete)
        {
            FailNextDelete = false;
            throw new ObjectStorageUnavailable("Synthetic delete failure.");
        }

        DeletedKeys.Add(key);
        _objects.Remove(key);
        return Task.CompletedTask;
    }

    private sealed class ObservedMemoryStream(byte[] bytes, Action disposed) : MemoryStream(bytes)
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing) disposed();
            base.Dispose(disposing);
        }
    }
}
