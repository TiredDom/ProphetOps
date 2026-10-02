using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProphetOps.Data;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class DataProtectionKeyPersistenceTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "prophetops-dp-tests-" + Guid.NewGuid().ToString("N"));

    public DataProtectionKeyPersistenceTests()
    {
        Directory.CreateDirectory(_storageRoot);
    }

    [Fact]
    public void Postgres_provider_configures_postgres_xml_repository()
    {
        var config = Config(
            ("Database:Provider", "postgres"),
            ("ConnectionStrings:Default", "Host=localhost;Database=prophetops"),
            ("Storage:Root", _storageRoot));

        var services = BuildServices(config);
        using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        Assert.NotNull(options.XmlRepository);
        Assert.IsType<PostgresXmlRepository>(options.XmlRepository);
    }

    [Fact]
    public void Sqlite_provider_configures_filesystem_xml_repository()
    {
        var config = Config(
            ("Database:Provider", "sqlite"),
            ("Storage:Root", _storageRoot));

        var services = BuildServices(config);
        using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        Assert.NotNull(options.XmlRepository);
        Assert.IsType<FileSystemXmlRepository>(options.XmlRepository);
    }

    [Fact]
    public async Task Key_reuse_across_independent_service_providers_preserves_protection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.EnsureCreatedAsync();
        }

        const string plaintext = "confidential-staff-session-data";
        string protectedData;

        // Provider 1: Generates key and protects data
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connection));
            services.AddDataProtection().SetApplicationName("ProphetOps");
            services.AddSingleton<PostgresXmlRepository>();
            services.AddOptions<KeyManagementOptions>().Configure<IServiceProvider>((options, sp) =>
                options.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>());

            using var sp1 = services.BuildServiceProvider();
            var protector1 = sp1.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose");
            protectedData = protector1.Protect(plaintext);
        }

        // Verify key was persisted to the database table
        await using (var verifyDb = new AppDbContext(dbOptions))
        {
            var keys = await verifyDb.DataProtectionKeys.AsNoTracking().ToListAsync();
            Assert.Single(keys);
            Assert.False(string.IsNullOrWhiteSpace(keys[0].FriendlyName));
            Assert.False(string.IsNullOrWhiteSpace(keys[0].Xml));
        }

        // Provider 2: Completely independent instance, unprotects data using keys from database
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connection));
            services.AddDataProtection().SetApplicationName("ProphetOps");
            services.AddSingleton<PostgresXmlRepository>();
            services.AddOptions<KeyManagementOptions>().Configure<IServiceProvider>((options, sp) =>
                options.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>());

            using var sp2 = services.BuildServiceProvider();
            var protector2 = sp2.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose");
            var roundtrip = protector2.Unprotect(protectedData);

            Assert.Equal(plaintext, roundtrip);
        }
    }

    [Fact]
    public void Postgres_xml_repository_fails_clearly_when_database_is_unavailable()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite("Data Source=nonexistent-invalid-path/missing.db"));
        using var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = sp.GetRequiredService<ILogger<PostgresXmlRepository>>();

        var repository = new PostgresXmlRepository(scopeFactory, logger);

        var getError = Assert.Throws<InvalidOperationException>(() => repository.GetAllElements());
        Assert.Contains("Failed to read Data Protection keys from PostgreSQL database", getError.Message);

        var element = new XElement("key", new XElement("data", "test-secret"));
        var storeError = Assert.Throws<InvalidOperationException>(() => repository.StoreElement(element, "key-test-id"));
        Assert.Contains("Failed to persist Data Protection key 'key-test-id' to PostgreSQL database", storeError.Message);

        // Verify key XML is not included in the exception message
        Assert.DoesNotContain("test-secret", storeError.Message);
        Assert.DoesNotContain("<key>", storeError.Message);
        Assert.Null(storeError.InnerException);
    }

    [Fact]
    public void Logging_and_exceptions_redact_sensitive_exception_text_key_xml_and_credentials()
    {
        const string fakePassword = "SuperSecretPassword123!";
        const string fakeKeyXml = "<key id=\"test-id\"><encryptedSecret>SecretKeyDataPayload==</encryptedSecret></key>";

        var syntheticException = new InvalidOperationException(
            $"Connection failed: Server=pg.agency.internal;Database=prophetops;Password={fakePassword}; " +
            $"Failed to insert entity: {fakeKeyXml}");

        var testLogger = new TestLogger<PostgresXmlRepository>();
        var failingScopeFactory = new FailingScopeFactory(syntheticException);
        var repository = new PostgresXmlRepository(failingScopeFactory, testLogger);

        // Test GetAllElements
        var readError = Assert.Throws<InvalidOperationException>(() => repository.GetAllElements());
        Assert.Contains("Failed to read Data Protection keys from PostgreSQL database", readError.Message);
        Assert.Contains("InvalidOperationException", readError.Message);
        Assert.DoesNotContain(fakePassword, readError.Message);
        Assert.DoesNotContain("SecretKeyDataPayload==", readError.Message);
        Assert.Null(readError.InnerException);

        // Test StoreElement
        var element = XElement.Parse(fakeKeyXml);
        var storeError = Assert.Throws<InvalidOperationException>(() => repository.StoreElement(element, "key-test-guid"));
        Assert.Contains("Failed to persist Data Protection key 'key-test-guid' to PostgreSQL database", storeError.Message);
        Assert.Contains("InvalidOperationException", storeError.Message);
        Assert.DoesNotContain(fakePassword, storeError.Message);
        Assert.DoesNotContain("SecretKeyDataPayload==", storeError.Message);
        Assert.Null(storeError.InnerException);

        // Verify all logged entries
        Assert.NotEmpty(testLogger.Logs);
        foreach (var log in testLogger.Logs)
        {
            Assert.DoesNotContain(fakePassword, log);
            Assert.DoesNotContain("SecretKeyDataPayload==", log);
            Assert.DoesNotContain("<key", log);
        }
    }

    private ServiceCollection BuildServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddSingleton<IHostEnvironment>(new TestEnvironment(_storageRoot));
        services.AddSingleton(sp => StoragePaths.FromConfiguration(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IHostEnvironment>()));
        services.AddSingleton(sp =>
        {
            var storage = sp.GetRequiredService<StoragePaths>();
            return DatabaseRuntimeOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>(), storage.DatabaseConnectionString);
        });
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var database = sp.GetRequiredService<DatabaseRuntimeOptions>();
            DatabaseConfiguration.Configure(options, database.Provider, database.ConnectionString, database.MigrationsAssembly);
        });

        services.AddDataProtection()
            .SetApplicationName("ProphetOps");
        services.AddSingleton<PostgresXmlRepository>();
        services.AddOptions<KeyManagementOptions>().Configure<DatabaseRuntimeOptions, StoragePaths, IServiceProvider>((options, database, paths, sp) =>
        {
            if (database.Provider == DatabaseProviderKind.Postgres)
            {
                options.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>();
            }
            else
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(paths.KeysPath), NullLoggerFactory.Instance);
            }
        });

        return services;
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => pair.Value)).Build();

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ProphetOps.Api.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Logs { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Logs.Add(message);
        }
    }

    private sealed class FailingScopeFactory(Exception exceptionToThrow) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw exceptionToThrow;
    }
}
