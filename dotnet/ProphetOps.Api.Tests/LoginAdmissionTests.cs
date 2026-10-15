using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Api;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class LoginAdmissionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "prophetops-login-admission-" + Guid.NewGuid().ToString("N"));

    public LoginAdmissionTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Fresh_login_state_rejects_password_reset_after_initial_verification()
    {
        const string password = "Original-login-password-581!";
        var dbPath = Path.Combine(_directory, "state.db");
        await using (var setup = Context(dbPath))
        {
            await setup.Database.MigrateAsync();
            setup.Users.Add(new User
            {
                Name = "Owner",
                Email = "owner@agency.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = Roles.OwnerManagement,
            });
            await setup.SaveChangesAsync();
        }

        await using var loginContext = Context(dbPath);
        var initiallyVerified = await loginContext.Users.SingleAsync(u => u.Email == "owner@agency.test");
        Assert.True(BCrypt.Net.BCrypt.Verify(password, initiallyVerified.PasswordHash));

        await using (var reset = Context(dbPath))
        {
            var user = await reset.Users.SingleAsync(u => u.Email == "owner@agency.test");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Reset-login-password-581!");
            user.SessionVersion++;
            await reset.SaveChangesAsync();
        }

        var fresh = await LoginAdmission.LoadFreshAuthorizedUserAsync(
            loginContext,
            initiallyVerified,
            password,
            CancellationToken.None);

        Assert.Null(fresh);
    }

    [Fact]
    public async Task Login_writer_lock_contention_returns_conflict_without_cookie()
    {
        using var factory = new LoginFactory(_directory);
        using var client = factory.CreateClient();
        await using var locked = new SqliteConnection($"Data Source={factory.DatabasePath};Pooling=False;Default Timeout=5");
        await locked.OpenAsync();
        await using var transaction = locked.BeginTransaction(deferred: false);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "owner@prophetops.local", password = "owner123" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain(response.Headers, header => string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase));
        await transaction.RollbackAsync();
    }

    private static AppDbContext Context(string dbPath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath};Pooling=False;Default Timeout=5")
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private sealed class LoginFactory(string directory) : WebApplicationFactory<Program>
    {
        public string DatabasePath => Path.Combine(directory, "login.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(directory);
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Enabled"] = "false",
                ["Storage:Root"] = Path.Combine(directory, "storage"),
                ["Business:TimeZone"] = "Asia/Manila",
            }));
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>) || d.ServiceType == typeof(DbContextOptions)).ToList())
                    services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={DatabasePath};Pooling=False;Default Timeout=5"));
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            using var db = Context(DatabasePath);
            db.Database.Migrate();
            if (db.Users.Any()) return;
            db.Users.Add(new User
            {
                Name = "Owner",
                Email = "owner@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
                Role = Roles.OwnerManagement,
            });
            db.SaveChanges();
        }
    }
}
