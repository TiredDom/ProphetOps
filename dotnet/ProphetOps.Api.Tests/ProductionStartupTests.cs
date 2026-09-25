using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

[CollectionDefinition(ProductionStartupCollection.Name, DisableParallelization = true)]
public sealed class ProductionStartupCollection
{
    public const string Name = "Production startup";
}

[Collection(ProductionStartupCollection.Name)]
public class ProductionStartupTests
{
    [Theory]
    [InlineData("Production", null)]
    [InlineData("Production", false)]
    [InlineData("Development", null)]
    public void Ordinary_startup_does_not_create_demonstration_records(string environment, bool? demo)
    {
        using var factory = new StartupFactory(environment, demo);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Empty(db.Users);
        Assert.Empty(db.Bookings);
        Assert.Empty(db.TravelPackages);
        Assert.Empty(db.Expenses);
        Assert.Empty(db.AuditEntries);
    }

    [Fact]
    public void Production_rejects_demo_configuration_before_changing_records()
    {
        using var factory = new StartupFactory("Production", true);
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("demonstration", error.Message, StringComparison.OrdinalIgnoreCase);
        using var db = factory.OpenContext();
        Assert.Empty(db.Users);
    }

    [Fact]
    public void Hosted_startup_rejects_missing_storage_root_before_serving()
    {
        using var factory = new StartupFactory("Production", null, includeStorage: false, hosted: true);
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains(Flatten(error), message => message.Contains("Storage:Root", StringComparison.Ordinal));
    }

    [Fact]
    public void Startup_rejects_missing_business_timezone_before_serving()
    {
        using var factory = new StartupFactory("Production", null, includeBusinessTimeZone: false);
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Business:TimeZone", error.Message);
    }

    [Fact]
    public void Hosted_preview_can_explicitly_disable_scheduled_backups()
    {
        using var factory = new StartupFactory("Production", null, hosted: true, overrides: HostedAccess()
            .Append(new KeyValuePair<string, string?>("Backup:Scheduled:Enabled", "false"))
            .ToDictionary(pair => pair.Key, pair => pair.Value));
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<AppDbContext>());
    }

    [Fact]
    public void Hosted_scheduled_backups_require_off_host_storage_selection_before_serving()
    {
        var overrides = HostedAccess()
            .Append(new KeyValuePair<string, string?>("Backup:Scheduled:Enabled", "true"))
            .Append(new KeyValuePair<string, string?>("Backup:Encryption:Key", TestKey()))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        using var factory = new StartupFactory("Production", null, hosted: true, overrides: overrides);
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Backup:Storage", error.Message);
    }

    [Fact]
    public void Hosted_scheduled_backups_require_encryption_key_even_when_r2_is_configured()
    {
        var overrides = HostedAccess()
            .Concat(R2Storage())
            .Append(new KeyValuePair<string, string?>("Backup:Scheduled:Enabled", "true"))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        using var factory = new StartupFactory("Production", null, hosted: true, overrides: overrides);
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Backup:Encryption:Key", error.Message);
    }

    [Fact]
    public void Development_can_explicitly_enable_demonstration_records()
    {
        using var factory = new StartupFactory("Development", true);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(db.Users, user => user.Email == "owner@prophetops.local");
        Assert.NotEmpty(db.Bookings);
    }

    [Fact]
    public void Production_preserves_existing_records_when_the_audit_table_is_empty()
    {
        using var factory = new StartupFactory("Production", null, db =>
        {
            db.Users.Add(new User { Name = "Agency owner", Email = "owner@agency.test", Role = Roles.OwnerManagement, PasswordHash = "unchanged-hash" });
            db.TravelPackages.Add(new TravelPackage { Code = "EXISTING", PackageName = "Existing package", AvailableSlots = 8 });
            db.Bookings.AddRange(Enumerable.Range(1, 8).Select(i => new Booking
            {
                Code = $"EXISTING-{i}", BookingDate = new DateOnly(2026, 7, i),
                Client = "Existing client", GrossRevenue = 1000 + i,
            }));
            db.SaveChanges();
        });
        using var before = factory.OpenContext();
        var rows = before.Bookings.AsNoTracking().OrderBy(b => b.Id)
            .Select(b => new { b.Id, b.Code, b.Client, b.GrossRevenue, b.VoidedAt, b.VoidedBy, b.VoidReason }).ToArray();

        using var client = factory.CreateClient();
        using var after = factory.OpenContext();
        Assert.Equal(rows, after.Bookings.AsNoTracking().OrderBy(b => b.Id)
            .Select(b => new { b.Id, b.Code, b.Client, b.GrossRevenue, b.VoidedAt, b.VoidedBy, b.VoidReason }).ToArray());
        Assert.Equal("unchanged-hash", Assert.Single(after.Users).PasswordHash);
        Assert.Equal(8, Assert.Single(after.TravelPackages).AvailableSlots);
        Assert.Empty(after.AuditEntries);
    }

    private sealed class StartupFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "prophetops-startup-storage-" + Guid.NewGuid().ToString("N"));
        private readonly string _environment;
        private readonly bool? _demo;
        private readonly bool _includeStorage;
        private readonly bool _includeBusinessTimeZone;
        private readonly bool _hosted;
        private readonly Dictionary<string, string?> _overrides;

        public StartupFactory(string environment, bool? demo, Action<AppDbContext>? prepare = null,
            bool includeStorage = true, bool includeBusinessTimeZone = true, bool hosted = false,
            Dictionary<string, string?>? overrides = null)
        {
            _environment = environment;
            _demo = demo;
            _includeStorage = includeStorage;
            _includeBusinessTimeZone = includeBusinessTimeZone;
            _hosted = hosted;
            _overrides = overrides ?? [];
            Directory.CreateDirectory(_storageRoot);
            _connection.Open();
            using var db = OpenContext();
            db.Database.Migrate();
            prepare?.Invoke(db);
        }

        public AppDbContext OpenContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = _demo?.ToString(),
                    ["Hosted:Enabled"] = _hosted.ToString(),
                };
                if (_includeStorage) values["Storage:Root"] = _storageRoot;
                if (_includeBusinessTimeZone) values["Business:TimeZone"] = "Asia/Manila";
                foreach (var (key, value) in _overrides) values[key] = value;
                config.AddInMemoryCollection(values);
            });
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(DbContextOptions)).ToList()) services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection.Dispose();
                if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
            }
        }
    }

    private static Dictionary<string, string?> HostedAccess() => new()
    {
        ["CloudflareAccess:Enabled"] = "true",
        ["CloudflareAccess:Issuer"] = "https://agency.cloudflareaccess.com",
        ["CloudflareAccess:Audience"] = "audience",
        ["CloudflareAccess:JwksUrl"] = "https://access.example.test/certs",
    };

    private static Dictionary<string, string?> R2Storage() => new()
    {
        ["Backup:Storage"] = "r2",
        ["Backup:S3:Endpoint"] = "https://account.r2.cloudflarestorage.com",
        ["Backup:S3:Bucket"] = "prophetops-backups",
        ["Backup:S3:Region"] = "auto",
        ["Backup:S3:AccessKeyId"] = "access-key",
        ["Backup:S3:SecretAccessKey"] = "secret-key",
        ["Backup:S3:Prefix"] = "tests/",
    };

    private static string TestKey() => Convert.ToBase64String(Enumerable.Repeat((byte)42, 32).ToArray());

    private static IReadOnlyList<string> Flatten(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return messages;
    }
}
