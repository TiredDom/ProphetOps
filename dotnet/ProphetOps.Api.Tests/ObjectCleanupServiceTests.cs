using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class ObjectCleanupServiceTests
{
    [Fact]
    public async Task Cleanup_rechecks_live_references_before_deleting()
    {
        await using var fixture = await CleanupFixture.Create();
        fixture.Objects.Store("packages/live.png", [1], "image/png");
        await fixture.AddCleanup("packages/live.png", ObjectCleanupReasons.PackageImageDeleted);
        await fixture.Reference("packages/live.png");

        await fixture.Service.RunOnceAsync(CancellationToken.None);

        Assert.True(fixture.Objects.Contains("packages/live.png"));
        Assert.Empty(fixture.Objects.DeletedKeys);
    }

    [Fact]
    public async Task Cleanup_retains_objects_when_recovery_dependency_is_unknown()
    {
        await using var fixture = await CleanupFixture.Create();
        fixture.Retention.Decision = ObjectRetentionDecision.Unknown;
        fixture.Objects.Store("packages/unknown.png", [1], "image/png");
        await fixture.AddCleanup("packages/unknown.png", ObjectCleanupReasons.PackageImageReplaced);

        await fixture.Service.RunOnceAsync(CancellationToken.None);

        Assert.True(fixture.Objects.Contains("packages/unknown.png"));
        Assert.Empty(fixture.Objects.DeletedKeys);
    }

    [Fact]
    public async Task Cleanup_failure_is_retained_for_retry_without_losing_the_entry()
    {
        await using var fixture = await CleanupFixture.Create();
        fixture.Objects.Store("packages/retry.png", [1], "image/png");
        fixture.Objects.FailNextDelete = true;
        await fixture.AddCleanup("packages/retry.png", ObjectCleanupReasons.PackageImageDeleted);

        await fixture.Service.RunOnceAsync(CancellationToken.None);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.ObjectCleanupEntries.SingleAsync();
        Assert.Equal(1, entry.Attempts);
        Assert.Contains("Synthetic delete failure", entry.LastError);
        Assert.True(fixture.Objects.Contains("packages/retry.png"));
    }

    [Fact]
    public async Task Cleanup_is_deferred_when_maintenance_gate_is_closed_for_backup()
    {
        var gate = new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance);
        await using var fixture = await CleanupFixture.Create(gate);
        fixture.Objects.Store("packages/deferred.png", [1], "image/png");
        await fixture.AddCleanup("packages/deferred.png", ObjectCleanupReasons.PackageImageDeleted);

        var lease = await gate.TryBeginBackupCaptureAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(lease);
        using (lease)
        {
            await fixture.Service.RunOnceAsync(CancellationToken.None);
        }

        Assert.True(fixture.Objects.Contains("packages/deferred.png"));
        Assert.Empty(fixture.Objects.DeletedKeys);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.ObjectCleanupEntries.SingleAsync();
        Assert.Equal(0, entry.Attempts);
    }

    private sealed class CleanupFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private CleanupFixture(ServiceProvider services, SqliteConnection connection)
        {
            Services = services;
            _connection = connection;
            Objects = (RecordingObjectStorage)services.GetRequiredService<IObjectStorage>();
            Retention = (RecordingRetentionPolicy)services.GetRequiredService<IObjectRetentionPolicy>();
            Service = services.GetRequiredService<ObjectCleanupService>();
        }

        public ServiceProvider Services { get; }
        public RecordingObjectStorage Objects { get; }
        public RecordingRetentionPolicy Retention { get; }
        public ObjectCleanupService Service { get; }

        public static async Task<CleanupFixture> Create(MaintenanceGate? gate = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
                .AddSingleton<IObjectStorage, RecordingObjectStorage>()
                .AddSingleton<IObjectRetentionPolicy, RecordingRetentionPolicy>()
                .AddSingleton(TimeProvider.System)
                .AddSingleton<ILogger<ObjectCleanupService>>(NullLogger<ObjectCleanupService>.Instance);
            if (gate is not null)
            {
                services.AddSingleton(gate);
            }
            services.AddTransient<ObjectCleanupService>();
            var sp = services.BuildServiceProvider();
            await using var scope = sp.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            return new CleanupFixture(sp, connection);
        }

        public async Task AddCleanup(string key, string reason)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ObjectCleanupEntries.Add(ObjectCleanupEntry.Create(key, DateTimeOffset.UtcNow.AddMinutes(-1), reason));
            await db.SaveChangesAsync();
        }

        public async Task Reference(string key)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TravelPackages.Add(new TravelPackage
            {
                Code = "REF",
                PackageName = "Referenced",
                Destination = "Test",
                BasePrice = 1,
                AvailableSlots = 1,
                SoldCount = 0,
                ReservedCount = 0,
                Status = "Normal",
                ImagePath = key,
            });
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}

public sealed class RecordingRetentionPolicy : IObjectRetentionPolicy
{
    public ObjectRetentionDecision Decision { get; set; } = ObjectRetentionDecision.SafeToDelete;

    public Task<ObjectRetentionDecision> CanDeleteAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(Decision);
}
