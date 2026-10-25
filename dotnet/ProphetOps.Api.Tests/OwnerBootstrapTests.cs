using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class OwnerBootstrapTests : IDisposable
{
    private const string ConfigurationName = "Configuration owner";
    private const string ConfigurationEmail = "configuration@agency.test";
    private const string ConfigurationPassword = "Configuration-test-password-581!";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "prophetops-bootstrap-" + Guid.NewGuid().ToString("N"));

    public OwnerBootstrapTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Command_creates_one_owner_without_starting_the_http_server_and_cannot_be_repeated()
    {
        using var occupiedPort = new TcpListener(IPAddress.Loopback, 0);
        occupiedPort.Start();
        var port = ((IPEndPoint)occupiedPort.LocalEndpoint).Port;
        const string password = "Only-for-bootstrap-tests-284!";
        var first = await Run("Agency owner", " OWNER@AGENCY.TEST ", password, port);
        Assert.Equal(0, first.ExitCode);
        Assert.DoesNotContain(password, first.Output);

        using var db = OpenContext();
        var owner = Assert.Single(db.Users.AsNoTracking());
        Assert.Equal("owner@agency.test", owner.Email);
        Assert.Equal(Roles.OwnerManagement, owner.Role);
        Assert.Equal("Active", owner.Status);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, owner.PasswordHash));
        Assert.Empty(db.Bookings);
        Assert.Empty(db.TravelPackages);

        var repeated = await Run("Other owner", "other@agency.test", "Another-test-password-284!", port);
        Assert.NotEqual(0, repeated.ExitCode);
        Assert.DoesNotContain("Another-test-password-284!", repeated.Output);
        Assert.Equal(owner.PasswordHash, Assert.Single(db.Users.AsNoTracking()).PasswordHash);
    }

    [Fact]
    public async Task Managed_startup_bootstrap_creates_one_owner_from_environment_without_manual_shell_access()
    {
        const string password = "Managed-startup-password-581!";
        var port = FreePort();
        var output = await RunManagedStartup("Managed owner", "managed@agency.test", password, port);

        Assert.DoesNotContain(password, output);
        using var db = OpenContext();
        var owner = Assert.Single(db.Users.AsNoTracking());
        Assert.Equal("managed@agency.test", owner.Email);
        Assert.Equal(Roles.OwnerManagement, owner.Role);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, owner.PasswordHash));
    }

    [Fact]
    public async Task Managed_startup_bootstrap_skips_existing_account_without_resetting_it()
    {
        const string originalPassword = "Original-managed-password-581!";
        using (var db = OpenContext())
        {
            db.Database.Migrate();
            db.Users.Add(new User
            {
                Name = "Existing owner",
                Email = "existing@agency.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(originalPassword),
                Role = Roles.OwnerManagement,
                Status = "Active",
            });
            db.SaveChanges();
        }

        var output = await RunManagedStartup("New owner", "new@agency.test", "New-managed-password-581!", FreePort());

        Assert.DoesNotContain("New-managed-password-581!", output);
        using var after = OpenContext();
        var owner = Assert.Single(after.Users.AsNoTracking());
        Assert.Equal("existing@agency.test", owner.Email);
        Assert.Equal("Existing owner", owner.Name);
        Assert.True(BCrypt.Net.BCrypt.Verify(originalPassword, owner.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("New-managed-password-581!", owner.PasswordHash));
    }

    [Fact]
    public async Task Managed_startup_bootstrap_rejects_invalid_empty_database_setup()
    {
        var result = await RunManagedStartupToExit("Managed owner", "managed@agency.test", "short", FreePort());

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("short", result.Output);
        using var db = OpenContext();
        db.Database.Migrate();
        Assert.Empty(db.Users);
    }

    [Theory]
    [InlineData("", "owner@agency.test", "Valid-test-password-284!")]
    [InlineData("Agency owner", "invalid-email", "Valid-test-password-284!")]
    [InlineData("Agency owner", "owner@agency.test", "short")]
    public async Task Invalid_input_fails_without_creating_an_account(string name, string email, string password)
    {
        var result = await Run(name, email, password, 0);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Owner setup", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, result.Output);
        using var db = OpenContext();
        db.Database.Migrate();
        Assert.Empty(db.Users);
    }

    [Theory]
    [InlineData(false, "Name")]
    [InlineData(false, "Email")]
    [InlineData(false, "Password")]
    [InlineData(true, "Name")]
    [InlineData(true, "Email")]
    [InlineData(true, "Password")]
    public async Task Configuration_credentials_cannot_replace_a_missing_environment_secret(bool useJson, string missing)
    {
        const string password = "Environment-test-password-581!";
        var result = await Run(
            missing == "Name" ? null : "Environment owner",
            missing == "Email" ? null : "environment@agency.test",
            missing == "Password" ? null : password,
            0, start => AddConfigurationCredentials(start, useJson));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Owner setup", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, result.Output);
        Assert.DoesNotContain(ConfigurationPassword, result.Output);
        using var db = OpenContext();
        db.Database.Migrate();
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Command_line_credentials_cannot_override_environment_secrets()
    {
        const string password = "Environment-test-password-581!";
        var result = await Run("Environment owner", "environment@agency.test", password, 0,
            start => AddConfigurationCredentials(start, useJson: false));

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain(password, result.Output);
        Assert.DoesNotContain(ConfigurationPassword, result.Output);
        using var db = OpenContext();
        var owner = Assert.Single(db.Users.AsNoTracking());
        Assert.Equal("Environment owner", owner.Name);
        Assert.Equal("environment@agency.test", owner.Email);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, owner.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify(ConfigurationPassword, owner.PasswordHash));
    }

    [Fact]
    public async Task Migration_command_reaches_database_configuration_before_object_storage_credentials()
    {
        var result = await RunMigrate(start =>
        {
            start.Environment["Hosted__Enabled"] = "true";
            start.Environment["Hosted__AccessMode"] = "ApplicationLogin";
            start.Environment["Database__Provider"] = "postgres";
            start.Environment["ConnectionStrings__Default"] = "Host=localhost;Database=runtime";
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ConnectionStrings:Maintenance", result.Output);
        Assert.DoesNotContain("ObjectStorage", result.Output);
    }

    private void AddConfigurationCredentials(ProcessStartInfo start, bool useJson)
    {
        if (useJson)
        {
            File.WriteAllText(Path.Combine(_directory, "appsettings.Production.json"), JsonSerializer.Serialize(new
            {
                Bootstrap = new
                {
                    OwnerName = ConfigurationName,
                    OwnerEmail = ConfigurationEmail,
                    OwnerPassword = ConfigurationPassword,
                },
            }));
        }
        else
        {
            start.ArgumentList.Add("--Bootstrap:OwnerName=" + ConfigurationName);
            start.ArgumentList.Add("--Bootstrap:OwnerEmail=" + ConfigurationEmail);
            start.ArgumentList.Add("--Bootstrap:OwnerPassword=" + ConfigurationPassword);
        }
    }

    private async Task<(int ExitCode, string Output)> Run(string? name, string? email, string? password, int port,
        Action<ProcessStartInfo>? configure = null)
    {
        var start = StartInfo(port);
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("--bootstrap-owner");
        start.ArgumentList.Add("--contentRoot=" + _directory);
        if (name is not null) start.Environment["Bootstrap__OwnerName"] = name;
        if (email is not null) start.Environment["Bootstrap__OwnerEmail"] = email;
        if (password is not null) start.Environment["Bootstrap__OwnerPassword"] = password;
        configure?.Invoke(start);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        return (process.ExitCode, await stdout + await stderr);
    }

    private async Task<(int ExitCode, string Output)> RunMigrate(Action<ProcessStartInfo>? configure = null)
    {
        var start = StartInfo(0);
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("--migrate-database");
        start.ArgumentList.Add("--contentRoot=" + _directory);
        configure?.Invoke(start);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        return (process.ExitCode, await stdout + await stderr);
    }

    private async Task<string> RunManagedStartup(string name, string email, string password, int port)
    {
        var start = StartInfo(port);
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("--contentRoot=" + _directory);
        start.Environment["Bootstrap__Owner__Enabled"] = "true";
        start.Environment["Bootstrap__OwnerName"] = name;
        start.Environment["Bootstrap__OwnerEmail"] = email;
        start.Environment["Bootstrap__OwnerPassword"] = password;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        using var http = new HttpClient();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited) break;
            try
            {
                using var db = OpenContext();
                db.Database.Migrate();
                if (db.Users.AsNoTracking().Any())
                {
                    var health = await http.GetAsync($"http://127.0.0.1:{port}/health/live");
                    if (health.IsSuccessStatusCode) break;
                }
            }
            catch (Exception ex) when (ex is SqliteException or HttpRequestException)
            {
            }
            await Task.Delay(250);
        }

        if (!process.HasExited) process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
        return await stdout + await stderr;
    }

    private async Task<(int ExitCode, string Output)> RunManagedStartupToExit(string name, string email, string password, int port)
    {
        var start = StartInfo(port);
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("--contentRoot=" + _directory);
        start.Environment["Bootstrap__Owner__Enabled"] = "true";
        start.Environment["Bootstrap__OwnerName"] = name;
        start.Environment["Bootstrap__OwnerEmail"] = email;
        start.Environment["Bootstrap__OwnerPassword"] = password;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        return (process.ExitCode, await stdout + await stderr);
    }

    private ProcessStartInfo StartInfo(int port)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = _directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["Demo__Enabled"] = "false";
        start.Environment["ConnectionStrings__Default"] = ConnectionString;
        start.Environment["Storage__Root"] = Path.Combine(_directory, "storage");
        start.Environment["Business__TimeZone"] = "Asia/Manila";
        start.Environment["Urls"] = $"http://127.0.0.1:{port}";
        foreach (var key in new[] { "Bootstrap__OwnerName", "Bootstrap__OwnerEmail", "Bootstrap__OwnerPassword", "Bootstrap__Owner__Enabled" })
            start.Environment.Remove(key);
        return start;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(_directory, "test.db"), Pooling = false,
    }.ToString();

    private AppDbContext OpenContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(ConnectionString).Options);

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
