using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class ManualBackupExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "prophetops-export-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] ValidKeyBytes = SHA256.HashData(Encoding.UTF8.GetBytes("test-key-material-for-export-32b"));
    private static readonly string ValidBase64Key = Convert.ToBase64String(ValidKeyBytes);

    public ManualBackupExportTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public async Task Export_Requires_Owner_Role_Denies_Anonymous_Admin_Staff()
    {
        using var factory = new ExportTestFactory(_root, ValidBase64Key);
        await SeedFixture(factory);

        // 1. Anonymous request -> 401 Unauthorized
        using var anon = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var anonResp = await anon.PostAsync("/api/maintenance/backup/export", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonResp.StatusCode);

        // 2. Admin request -> 403 Forbidden
        using var admin = await AuthenticatedClient.Login(factory, "admin@prophetops.local", "admin123");
        using var adminResp = await admin.PostAsync("/api/maintenance/backup/export", null);
        Assert.Equal(HttpStatusCode.Forbidden, adminResp.StatusCode);

        // 3. Staff request -> 403 Forbidden
        using var staff = await AuthenticatedClient.Login(factory, "staff@prophetops.local", "staff123");
        using var staffResp = await staff.PostAsync("/api/maintenance/backup/export", null);
        Assert.Equal(HttpStatusCode.Forbidden, staffResp.StatusCode);

        // 4. Owner request -> 200 OK
        using var owner = await AuthenticatedClient.Login(factory, "owner@prophetops.local", "owner123");
        using var ownerResp = await owner.PostAsync("/api/maintenance/backup/export", null);
        Assert.Equal(HttpStatusCode.OK, ownerResp.StatusCode);
    }

    [Fact]
    public async Task Export_Requires_Valid_Csrf()
    {
        using var factory = new ExportTestFactory(_root, ValidBase64Key);
        await SeedFixture(factory);

        using var client = factory.CreateClient();
        // Login as owner to get session cookie
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "owner@prophetops.local", password = "owner123" });
        login.EnsureSuccessStatusCode();

        // POST without X-XSRF-TOKEN header -> 400 Bad Request
        using var reqWithoutCsrf = new HttpRequestMessage(HttpMethod.Post, "/api/maintenance/backup/export");
        using var respNoCsrf = await client.SendAsync(reqWithoutCsrf);
        Assert.Equal(HttpStatusCode.BadRequest, respNoCsrf.StatusCode);

        // POST with bad X-XSRF-TOKEN -> 400 Bad Request
        using var reqBadCsrf = new HttpRequestMessage(HttpMethod.Post, "/api/maintenance/backup/export");
        reqBadCsrf.Headers.Add("X-XSRF-TOKEN", "invalid-token");
        using var respBadCsrf = await client.SendAsync(reqBadCsrf);
        Assert.Equal(HttpStatusCode.BadRequest, respBadCsrf.StatusCode);
    }

    [Fact]
    public async Task Export_Fresh_Session_Operator_Sequence_Works_Without_Helper_Hiding_Get()
    {
        var rootSeq = Path.Combine(_root, "fresh-session-seq");
        using var factory = new ExportTestFactory(rootSeq, ValidBase64Key);
        await SeedFixture(factory);

        // Explicitly create a fresh client with cookie handling
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        // Step 1: POST /api/auth/login
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "owner@prophetops.local",
            password = "owner123"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Step 2: GET /api/auth/me to issue fresh XSRF-TOKEN cookie (since POST login does not issue XSRF-TOKEN)
        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // Step 3: Extract and URI-decode XSRF token from Set-Cookie header
        Assert.True(meResponse.Headers.TryGetValues("Set-Cookie", out var setCookies));
        var xsrfCookie = setCookies.FirstOrDefault(c => c.StartsWith("XSRF-TOKEN="));
        Assert.NotNull(xsrfCookie);
        var rawToken = xsrfCookie.Split(';')[0]["XSRF-TOKEN=".Length..];
        var xsrfToken = Uri.UnescapeDataString(rawToken);
        Assert.False(string.IsNullOrWhiteSpace(xsrfToken));

        // Step 4: POST /api/maintenance/backup/export with X-XSRF-TOKEN header
        using var exportRequest = new HttpRequestMessage(HttpMethod.Post, "/api/maintenance/backup/export");
        exportRequest.Headers.Add("X-XSRF-TOKEN", xsrfToken);
        using var exportResponse = await client.SendAsync(exportRequest);

        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("application/octet-stream", exportResponse.Content.Headers.ContentType?.MediaType);
        Assert.True(exportResponse.Headers.CacheControl?.NoStore);

        var disposition = exportResponse.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.EndsWith(".prophetops-backup.pobak", disposition.FileName, StringComparison.OrdinalIgnoreCase);

        var bytes = await exportResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > BackupEncryptedEnvelope.HeaderLength);
        Assert.Equal("POBAK"u8.ToArray(), bytes.Take(5).ToArray());
    }

    [Fact]
    public async Task Export_Fails_Safely_When_Encryption_Not_Configured_Or_Invalid()
    {
        // 1. Unconfigured encryption key (null)
        var rootNoEnc = Path.Combine(_root, "no-enc");
        using (var factoryNoEnc = new ExportTestFactory(rootNoEnc, encryptionKey: null))
        {
            await SeedFixture(factoryNoEnc);
            using var owner = await AuthenticatedClient.Login(factoryNoEnc);
            using var resp = await owner.PostAsync("/api/maintenance/backup/export", null);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
            var content = await resp.Content.ReadAsStringAsync();
            Assert.Contains("unavailable", content, StringComparison.OrdinalIgnoreCase);
        }

        // 2. Invalid encryption key (corrupt base64 / wrong length)
        var rootBadEnc = Path.Combine(_root, "bad-enc");
        using (var factoryBadEnc = new ExportTestFactory(rootBadEnc, encryptionKey: "not-a-valid-key"))
        {
            await SeedFixture(factoryBadEnc);
            using var owner = await AuthenticatedClient.Login(factoryBadEnc);
            using var resp = await owner.PostAsync("/api/maintenance/backup/export", null);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        }
    }

    [Fact]
    public async Task Export_Fails_Safely_When_Gate_Is_Busy()
    {
        using var factory = new ExportTestFactory(_root, ValidBase64Key, drainTimeoutSeconds: 1);
        await SeedFixture(factory);
        using var owner = await AuthenticatedClient.Login(factory);

        using var scope = factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<MaintenanceGate>();

        // Acquire backup capture lock so the gate is closed
        using var activeCapture = await gate.TryBeginBackupCaptureAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(activeCapture);

        using var resp = await owner.PostAsync("/api/maintenance/backup/export", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
    }

    [Fact]
    public async Task Export_Does_Not_Invoke_IBackupStorage()
    {
        var throwingStorage = new ThrowingBackupStorage();
        using var factory = new ExportTestFactory(_root, ValidBase64Key, backupStorage: throwingStorage);
        await SeedFixture(factory);

        using var owner = await AuthenticatedClient.Login(factory);
        using var resp = await owner.PostAsync("/api/maintenance/backup/export", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(0, throwingStorage.UploadCallCount);
        Assert.Equal(0, throwingStorage.VerifyCallCount);
        Assert.Equal(0, throwingStorage.DeleteCallCount);
    }

    [Fact]
    public async Task Export_Success_Streams_Encrypted_Envelope_And_Decrypts_Correctly()
    {
        using var factory = new ExportTestFactory(_root, ValidBase64Key);
        await SeedFixture(factory);

        using var owner = await AuthenticatedClient.Login(factory);
        using var resp = await owner.PostAsync("/api/maintenance/backup/export", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        Assert.Equal("application/octet-stream", resp.Content.Headers.ContentType?.MediaType);
        Assert.True(resp.Headers.CacheControl?.NoStore);

        var disposition = resp.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.NotNull(disposition.FileName);
        Assert.EndsWith(".prophetops-backup.pobak", disposition.FileName, StringComparison.OrdinalIgnoreCase);

        var envelopeBytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.True(envelopeBytes.Length > BackupEncryptedEnvelope.HeaderLength);

        // Decrypt envelope with test key
        var decryptedZipBytes = BackupEncryptedEnvelope.Decrypt(envelopeBytes, ValidKeyBytes);
        using var zipStream = new MemoryStream(decryptedZipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("manifest.json");
        Assert.NotNull(manifestEntry);

        BackupManifest? manifest;
        using (var manifestStream = manifestEntry.Open())
        {
            manifest = JsonSerializer.Deserialize(manifestStream, BackupJsonContext.Default.BackupManifest);
        }
        Assert.NotNull(manifest);
        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("AES-256-GCM", manifest.Encryption.Mode);
        Assert.True(manifest.Counts.TravelPackages >= 1);
        Assert.True(manifest.Counts.Bookings >= 1);

        // Compare exact image bytes to fixture
        var expectedImageBytes = Png();
        var imageEntry = archive.GetEntry("uploads/packages/backup.png");
        Assert.NotNull(imageEntry);

        using (var imageStream = imageEntry.Open())
        using (var imageMs = new MemoryStream())
        {
            await imageStream.CopyToAsync(imageMs);
            var actualImageBytes = imageMs.ToArray();
            Assert.Equal(expectedImageBytes, actualImageBytes);
        }

        // Verify manifest image file record: exact length and SHA-256 match
        var manifestImageFile = manifest.Files.Single(f => f.Role == "package-image" && f.Path == "uploads/packages/backup.png");
        Assert.Equal((long)expectedImageBytes.Length, manifestImageFile.Size);
        var expectedSha256 = Convert.ToHexString(SHA256.HashData(expectedImageBytes)).ToLowerInvariant();
        Assert.Equal(expectedSha256, manifestImageFile.Sha256);

        // Verify database entry in the decrypted archive contains seeded records
        var dbEntry = archive.GetEntry("database/prophetops.db");
        Assert.NotNull(dbEntry);

        using (var dbEntryStream = dbEntry.Open())
        using (var dbMs = new MemoryStream())
        {
            await dbEntryStream.CopyToAsync(dbMs);
            var dbString = Encoding.UTF8.GetString(dbMs.ToArray());
            Assert.Contains("B04-PKG", dbString);
            Assert.Contains("B04-BOOK", dbString);
        }
    }

    [Fact]
    public async Task Export_Cleans_Up_Temporary_File_After_Successful_Stream()
    {
        using var factory = new ExportTestFactory(_root, ValidBase64Key);
        await SeedFixture(factory);

        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<StoragePaths>();

        using var owner = await AuthenticatedClient.Login(factory);
        using var resp = await owner.PostAsync("/api/maintenance/backup/export", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Read all stream bytes to complete response execution
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        // Staging directory should have NO lingering .pobak or .working files from this export
        var stagingFiles = Directory.GetFiles(storage.BackupStagingPath, "*.pobak", SearchOption.AllDirectories);
        Assert.Empty(stagingFiles);
    }

    [Fact]
    public async Task Export_Cleans_Up_Temporary_File_On_Aborted_Stream_Or_Disconnect()
    {
        // Test AutoDeletingFileResult directly with a simulated disconnected/aborted stream
        var tempStagedFile = Path.Combine(_root, "test-abort-" + Guid.NewGuid().ToString("N") + ".prophetops-backup.pobak");
        await File.WriteAllBytesAsync(tempStagedFile, new byte[1024 * 64]);
        Assert.True(File.Exists(tempStagedFile));

        var result = new AutoDeletingFileResult(tempStagedFile, "application/octet-stream", Path.GetFileName(tempStagedFile));

        var httpContext = new DefaultHttpContext();
        var abortingStream = new AbortingMemoryStream(throwAfterBytes: 512);
        httpContext.Response.Body = abortingStream;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        // Stream writing should fail/abort
        await Assert.ThrowsAsync<IOException>(async () =>
        {
            await result.ExecuteResultAsync(actionContext);
        });

        // The temporary staged file MUST be promptly cleaned up on disconnect/failure!
        Assert.False(File.Exists(tempStagedFile));
    }

    [Fact]
    public async Task Export_Cleans_Up_If_File_Open_Fails()
    {
        var nonExistentPath = Path.Combine(_root, "non-existent-export.prophetops-backup.pobak");
        var result = new AutoDeletingFileResult(nonExistentPath, "application/octet-stream", "non-existent.pobak");

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await result.ExecuteResultAsync(actionContext);
        });

        Assert.False(File.Exists(nonExistentPath));
    }

    [Fact]
    public async Task Export_Reopens_Gate_Before_Streaming()
    {
        using var factory = new ExportTestFactory(_root, ValidBase64Key);
        await SeedFixture(factory);

        using var scope = factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<MaintenanceGate>();

        using var owner = await AuthenticatedClient.Login(factory);

        // Send export request and read stream with deliberate pause
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/maintenance/backup/export");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var resp = await owner.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // While response headers are received and before/during reading content:
        // Mutation gate MUST be open!
        var mutationAdmission = gate.TryEnterMutation();
        Assert.True(mutationAdmission.Allowed, "Mutation gate was not reopened before streaming started!");
        using var mutationLease = mutationAdmission.Lease;

        // Finish reading stream
        var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
        var buffer = new byte[1024];
        while (await stream.ReadAsync(buffer, cts.Token) > 0)
        {
        }
    }

    [Fact]
    public async Task Export_Preserves_Unrelated_Backups()
    {
        using var factory = new ExportTestFactory(_root, ValidBase64Key);
        await SeedFixture(factory);

        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<StoragePaths>();

        // Create an unrelated pre-existing backup in staging directory
        var unrelatedBackup = Path.Combine(storage.BackupStagingPath, "unrelated-existing-backup.prophetops-backup.pobak");
        await File.WriteAllTextAsync(unrelatedBackup, "preserve-me");

        using var owner = await AuthenticatedClient.Login(factory);
        using var resp = await owner.PostAsync("/api/maintenance/backup/export", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        // Assert unrelated pre-existing backup was NOT touched or deleted
        Assert.True(File.Exists(unrelatedBackup));
        Assert.Equal("preserve-me", await File.ReadAllTextAsync(unrelatedBackup));

        // Clean up test fixture
        File.Delete(unrelatedBackup);
    }

    [Fact]
    public async Task Export_Fails_Safely_And_Cleans_Up_When_Capture_Throws()
    {
        var rootFail = Path.Combine(_root, "fail-capture");
        var failingOps = new FailingPackageFileOperations(new IOException("Simulated disk fault during archive packaging"));
        using var factory = new ExportTestFactory(rootFail, ValidBase64Key, fileOperations: failingOps);
        await SeedFixture(factory);

        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<StoragePaths>();
        var gate = scope.ServiceProvider.GetRequiredService<MaintenanceGate>();

        // Pre-create an unrelated file in staging
        Directory.CreateDirectory(storage.BackupStagingPath);
        var unrelatedPath = Path.Combine(storage.BackupStagingPath, "unrelated-existing.pobak");
        await File.WriteAllTextAsync(unrelatedPath, "unrelated-data");

        using var owner = await AuthenticatedClient.Login(factory);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var resp = await owner.PostAsync("/api/maintenance/backup/export", null, cts.Token);

        // Must fail safely with 503
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);

        // Gate must be reopened
        var admission = gate.TryEnterMutation();
        Assert.True(admission.Allowed, "Maintenance gate was not reopened after capture failure!");
        using var lease = admission.Lease;

        // No lingering .working or .partial files owned by this failed capture
        var lingeringWorking = Directory.GetDirectories(storage.BackupStagingPath, "*.working", SearchOption.AllDirectories);
        Assert.Empty(lingeringWorking);
        var lingeringPartial = Directory.GetFiles(storage.BackupStagingPath, "*.partial", SearchOption.AllDirectories);
        Assert.Empty(lingeringPartial);

        // Unrelated file must remain intact
        Assert.True(File.Exists(unrelatedPath));
        Assert.Equal("unrelated-data", await File.ReadAllTextAsync(unrelatedPath, cts.Token));
    }

    [Fact]
    public async Task Export_Handles_Cancellation_Reopens_Gate_And_Cleans_Up()
    {
        var rootCancel = Path.Combine(_root, "cancel-capture");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cancellingOps = new CancellingPackageFileOperations(cts);

        using var factory = new ExportTestFactory(rootCancel, ValidBase64Key, fileOperations: cancellingOps);
        await SeedFixture(factory);

        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<StoragePaths>();
        var gate = scope.ServiceProvider.GetRequiredService<MaintenanceGate>();

        // Pre-create an unrelated file in staging
        Directory.CreateDirectory(storage.BackupStagingPath);
        var unrelatedPath = Path.Combine(storage.BackupStagingPath, "unrelated-existing-cancel.pobak");
        await File.WriteAllTextAsync(unrelatedPath, "unrelated-data-cancel", CancellationToken.None);

        using var owner = await AuthenticatedClient.Login(factory);

        // When cancellation is triggered in CreateZip, OperationCanceledException occurs
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/maintenance/backup/export");
            await owner.SendAsync(req, cts.Token);
        });

        // Gate must be reopened
        var admission = gate.TryEnterMutation();
        Assert.True(admission.Allowed, "Maintenance gate was not reopened after cancelled capture!");
        using var lease = admission.Lease;

        // No lingering .working or .partial files owned by this cancelled capture
        var lingeringWorking = Directory.GetDirectories(storage.BackupStagingPath, "*.working", SearchOption.AllDirectories);
        Assert.Empty(lingeringWorking);
        var lingeringPartial = Directory.GetFiles(storage.BackupStagingPath, "*.partial", SearchOption.AllDirectories);
        Assert.Empty(lingeringPartial);

        // Unrelated file must remain intact
        Assert.True(File.Exists(unrelatedPath));
        Assert.Equal("unrelated-data-cancel", await File.ReadAllTextAsync(unrelatedPath, CancellationToken.None));
    }

    private static async Task SeedFixture(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<StoragePaths>();
        Directory.CreateDirectory(storage.PackageImagesPath);
        Directory.CreateDirectory(storage.KeysPath);
        await File.WriteAllTextAsync(Path.Combine(storage.KeysPath, "key.xml"), "<key />");
        await File.WriteAllBytesAsync(storage.UploadedPackageImage("backup.png"), Png());

        var package = new TravelPackage
        {
            Code = "B04-PKG",
            PackageName = "B04 Package",
            Destination = "Bohol",
            BasePrice = 7000,
            AvailableSlots = 9,
            ImagePath = "backup.png",
        };
        db.TravelPackages.Add(package);
        db.Bookings.Add(new Booking
        {
            Code = "B04-BOOK",
            BookingDate = new DateOnly(2026, 9, 1),
            PassengerCount = 2,
            Client = "B04 Client",
            PackageName = "B04 Package",
            Destination = "Bohol",
            GrossRevenue = 7000,
            PaymentStatus = "Paid",
            BookingStatus = "Confirmed",
        });
        db.Expenses.Add(new Expense
        {
            Code = "B04-EXP",
            ExpenseDate = new DateOnly(2026, 9, 2),
            Category = "Transport",
            Amount = 1200,
            Notes = "Fuel",
        });
        await db.SaveChangesAsync();
    }

    private static byte[] Png() =>
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private sealed class ExportTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _root;
        private readonly string? _encryptionKey;
        private readonly int _drainTimeoutSeconds;
        private readonly IBackupStorage? _backupStorage;
        private readonly IBackupPackageFileOperations? _fileOperations;

        public ExportTestFactory(
            string root,
            string? encryptionKey = null,
            int drainTimeoutSeconds = 10,
            IBackupStorage? backupStorage = null,
            IBackupPackageFileOperations? fileOperations = null)
        {
            _root = root;
            _encryptionKey = encryptionKey;
            _drainTimeoutSeconds = drainTimeoutSeconds;
            _backupStorage = backupStorage;
            _fileOperations = fileOperations;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Enabled"] = "true",
                ["Storage:Root"] = _root,
                ["Business:TimeZone"] = "Asia/Manila",
                ["Backup:Keep"] = "1",
                ["Backup:Scheduled:Enabled"] = "false",
                ["Backup:DrainTimeoutSeconds"] = _drainTimeoutSeconds.ToString(),
                ["Backup:LocalStoragePath"] = Path.Combine(_root, "backups", "independent"),
                ["Hosted:Enabled"] = "false",
                ["CloudflareAccess:Enabled"] = "false",
                ["Backup:Encryption:Key"] = _encryptionKey,
                ["Backup:Encryption:KeyId"] = _encryptionKey is null ? null : "export-test-key",
            }));
            if (_backupStorage is not null)
            {
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IBackupStorage)).ToList())
                        services.Remove(descriptor);
                    services.AddScoped(_ => _backupStorage);
                });
            }
            if (_fileOperations is not null)
            {
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IBackupPackageFileOperations)).ToList())
                        services.Remove(descriptor);
                    services.AddScoped(_ => _fileOperations);
                });
            }
        }
    }

    private sealed class FailingPackageFileOperations : IBackupPackageFileOperations
    {
        private readonly Exception _exceptionToThrow;

        public FailingPackageFileOperations(Exception exceptionToThrow)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public void CreateZip(string sourceDirectory, string destination) => throw _exceptionToThrow;
        public void CreateZip(string sourceDirectory, string destination, long maxTotalBytes, long currentBaseBytes) => throw _exceptionToThrow;
        public Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, CancellationToken cancellationToken) => throw _exceptionToThrow;
    }

    private sealed class CancellingPackageFileOperations : IBackupPackageFileOperations
    {
        private readonly CancellationTokenSource _cts;

        public CancellingPackageFileOperations(CancellationTokenSource cts)
        {
            _cts = cts;
        }

        public void CreateZip(string sourceDirectory, string destination) =>
            CreateZip(sourceDirectory, destination, long.MaxValue, 0);

        public void CreateZip(string sourceDirectory, string destination, long maxTotalBytes, long currentBaseBytes)
        {
            _cts.Cancel();
            _cts.Token.ThrowIfCancellationRequested();
        }

        public Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, CancellationToken cancellationToken)
        {
            _cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromCanceled(cancellationToken);
        }
    }

    private sealed class ThrowingBackupStorage : IBackupStorage
    {
        public int UploadCallCount { get; private set; }
        public int VerifyCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }

        public Task<BackupUploadResult> UploadAsync(BackupPackage package, CancellationToken cancellationToken)
        {
            UploadCallCount++;
            throw new InvalidOperationException("IBackupStorage.UploadAsync should NOT be called for export.");
        }

        public Task<BackupVerificationResult> VerifyAsync(BackupUploadResult upload, BackupPackage source, CancellationToken cancellationToken)
        {
            VerifyCallCount++;
            throw new InvalidOperationException("IBackupStorage.VerifyAsync should NOT be called for export.");
        }

        public Task<IReadOnlyList<StoredBackup>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredBackup>>([]);

        public Task DeleteAsync(StoredBackup backup, CancellationToken cancellationToken)
        {
            DeleteCallCount++;
            throw new InvalidOperationException("IBackupStorage.DeleteAsync should NOT be called for export.");
        }

        public Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(null);
    }

    private sealed class AbortingMemoryStream : MemoryStream
    {
        private readonly int _throwAfterBytes;
        private int _bytesWritten;

        public AbortingMemoryStream(int throwAfterBytes)
        {
            _throwAfterBytes = throwAfterBytes;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _bytesWritten += count;
            if (_bytesWritten >= _throwAfterBytes)
            {
                throw new IOException("Simulated network disconnect / aborted client stream.");
            }
            await base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _bytesWritten += buffer.Length;
            if (_bytesWritten >= _throwAfterBytes)
            {
                throw new IOException("Simulated network disconnect / aborted client stream.");
            }
            await base.WriteAsync(buffer, cancellationToken);
        }
    }
}
