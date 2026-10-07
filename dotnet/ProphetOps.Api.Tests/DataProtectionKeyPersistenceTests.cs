using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
using ProphetOps.Api;
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

    [Fact]
    public void LoadCertificate_throws_when_base64_is_missing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.LoadCertificate("", "password"));
        Assert.Contains("DataProtection:CertificateBase64 is required", ex.Message);
    }

    [Fact]
    public void LoadCertificate_throws_when_password_is_missing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.LoadCertificate("c29tZS1iYXNlNjQ=", ""));
        Assert.Contains("DataProtection:CertificatePassword is required", ex.Message);
    }

    [Fact]
    public void LoadCertificate_throws_when_base64_is_malformed()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.LoadCertificate("not-valid-base64!!!", "password"));
        Assert.Contains("not a valid Base64 string", ex.Message);
    }

    [Fact]
    public void LoadCertificate_throws_when_password_is_wrong()
    {
        var (base64, _, _) = CreateTestRsaCertificatePfx("CorrectPassword123!");
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.LoadCertificate(base64, "WrongPassword!"));
        Assert.Contains("invalid password or corrupted certificate archive", ex.Message);
    }

    [Fact]
    public void LoadCertificate_throws_when_certificate_has_no_private_key()
    {
        var (base64, password) = CreateTestNoPrivateKeyPfx("Password123!");
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.LoadCertificate(base64, password));
        Assert.Contains("must contain a private key", ex.Message);
    }

    [Fact]
    public void LoadCertificate_throws_when_certificate_is_not_rsa()
    {
        var (base64, password) = CreateTestEcdsaCertificatePfx("Password123!");
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.LoadCertificate(base64, password));
        Assert.Contains("must contain an RSA private key", ex.Message);
    }

    [Fact]
    public void LoadCertificate_throws_when_rsa_key_size_is_less_than_2048()
    {
        var (base64, password, _) = CreateTestRsaCertificatePfx("Password123!", keySize: 1024);
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.LoadCertificate(base64, password));
        Assert.Contains("at least 2048 bits", ex.Message);
    }

    [Fact]
    public void LoadCertificate_throws_when_certificate_is_expired()
    {
        var (base64, password, _) = CreateTestRsaCertificatePfx(
            "Password123!",
            notBefore: DateTimeOffset.UtcNow.AddDays(-30),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.LoadCertificate(base64, password));
        Assert.Contains("has expired", ex.Message);
    }

    [Fact]
    public void LoadCertificate_throws_when_certificate_is_not_yet_valid()
    {
        var (base64, password, _) = CreateTestRsaCertificatePfx(
            "Password123!",
            notBefore: DateTimeOffset.UtcNow.AddDays(1),
            notAfter: DateTimeOffset.UtcNow.AddDays(30));
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.LoadCertificate(base64, password));
        Assert.Contains("not yet valid", ex.Message);
    }

    [Fact]
    public void LoadCertificate_does_not_leak_password_or_bytes_in_exception_messages()
    {
        const string secretPass = "TopSecretPassword987!";
        var (base64, _, _) = CreateTestRsaCertificatePfx("RealPassword!");
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.LoadCertificate(base64, secretPass));
        Assert.DoesNotContain(secretPass, ex.Message);
        Assert.DoesNotContain(base64[..20], ex.Message);
    }

    [Fact]
    public void ResolveCertificate_hosted_postgres_throws_when_certificate_base64_missing()
    {
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Database:Provider", "postgres"),
            ("ConnectionStrings:Default", "Host=localhost;Database=prophetops"));
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionConfiguration.ResolveCertificate(config, DatabaseProviderKind.Postgres, isHosted: true));
        Assert.Contains("Hosted PostgreSQL mode requires DataProtection:CertificateBase64", ex.Message);
    }

    [Fact]
    public void ResolveCertificate_hosted_postgres_throws_when_certificate_password_missing()
    {
        var (base64, _, _) = CreateTestRsaCertificatePfx("Password123!");
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Database:Provider", "postgres"),
            ("DataProtection:CertificateBase64", base64));
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionConfiguration.ResolveCertificate(config, DatabaseProviderKind.Postgres, isHosted: true));
        Assert.Contains("Hosted PostgreSQL mode requires DataProtection:CertificatePassword", ex.Message);
    }

    [Fact]
    public void ResolveCertificate_offline_commands_do_not_require_certificate()
    {
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Database:Provider", "postgres"));
        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection();
        // Should not throw when isOfflineCommand: true
        DataProtectionConfiguration.Configure(
            dpBuilder,
            services,
            config,
            DatabaseProviderKind.Postgres,
            isHosted: true,
            isOfflineCommand: true);
    }

    [Fact]
    public void ResolveCertificate_sqlite_provider_does_not_require_certificate()
    {
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Database:Provider", "sqlite"));
        var cert = DataProtectionConfiguration.ResolveCertificate(config, DatabaseProviderKind.Sqlite, isHosted: true);
        Assert.Null(cert);
    }

    [Fact]
    public async Task Persisted_xml_is_encrypted_at_rest_when_certificate_configured()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.EnsureCreatedAsync();
        }

        const string password = "TestPassword456!";
        var (base64, _, _) = CreateTestRsaCertificatePfx(password);
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Database:Provider", "postgres"),
            ("DataProtection:CertificateBase64", base64),
            ("DataProtection:CertificatePassword", password));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connection));
        var dp = services.AddDataProtection().SetApplicationName("ProphetOps");
        services.AddSingleton<PostgresXmlRepository>();
        services.AddOptions<KeyManagementOptions>().Configure<IServiceProvider>((options, sp) =>
            options.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>());
        DataProtectionConfiguration.Configure(dp, services, config, DatabaseProviderKind.Postgres, isHosted: true);

        using (var sp = services.BuildServiceProvider())
        {
            var protector = sp.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose");
            protector.Protect("test-secret-payload");
        }

        // Verify persisted XML contains encryptedSecret and does NOT contain masterKey or unencrypted key elements
        await using (var verifyDb = new AppDbContext(dbOptions))
        {
            var keys = await verifyDb.DataProtectionKeys.AsNoTracking().ToListAsync();
            Assert.Single(keys);
            var xml = keys[0].Xml;
            Assert.NotNull(xml);
            var doc = XDocument.Parse(xml);

            // Assert encryptedSecret element is present
            var encryptedSecret = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "encryptedSecret");
            Assert.NotNull(encryptedSecret);

            // Assert encrypted payload elements are present inside encryptedSecret
            var encryptedData = encryptedSecret.Descendants().FirstOrDefault(e => e.Name.LocalName == "EncryptedData");
            Assert.NotNull(encryptedData);
            var cipherValue = encryptedSecret.Descendants().FirstOrDefault(e => e.Name.LocalName == "CipherValue");
            Assert.NotNull(cipherValue);
            Assert.False(string.IsNullOrWhiteSpace(cipherValue.Value));

            // Assert NO plaintext master-key or secret elements exist
            Assert.DoesNotContain(doc.Descendants(), e => string.Equals(e.Name.LocalName, "masterKey", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(doc.Descendants(), e => string.Equals(e.Name.LocalName, "value", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("masterKey", xml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("unencrypted form", xml, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Cross_service_provider_with_certificate_encryption_round_trips_data()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.EnsureCreatedAsync();
        }

        const string password = "TestPassword456!";
        var (base64, _, _) = CreateTestRsaCertificatePfx(password);
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Database:Provider", "postgres"),
            ("DataProtection:CertificateBase64", base64),
            ("DataProtection:CertificatePassword", password));

        const string plaintext = "confidential-staff-session-data";
        string protectedData;

        // Provider 1: Generates encrypted key and protects data
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connection));
            var dp = services.AddDataProtection().SetApplicationName("ProphetOps");
            services.AddSingleton<PostgresXmlRepository>();
            services.AddOptions<KeyManagementOptions>().Configure<IServiceProvider>((options, sp) =>
                options.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>());
            DataProtectionConfiguration.Configure(dp, services, config, DatabaseProviderKind.Postgres, isHosted: true);

            using var sp1 = services.BuildServiceProvider();
            var protector1 = sp1.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose");
            protectedData = protector1.Protect(plaintext);
        }

        // Provider 2: Independent instance, unprotects data using keys from DB and same certificate
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connection));
            var dp = services.AddDataProtection().SetApplicationName("ProphetOps");
            services.AddSingleton<PostgresXmlRepository>();
            services.AddOptions<KeyManagementOptions>().Configure<IServiceProvider>((options, sp) =>
                options.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>());
            DataProtectionConfiguration.Configure(dp, services, config, DatabaseProviderKind.Postgres, isHosted: true);

            using var sp2 = services.BuildServiceProvider();
            var protector2 = sp2.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose");
            var roundtrip = protector2.Unprotect(protectedData);

            Assert.Equal(plaintext, roundtrip);
        }
    }

    [Fact]
    public async Task Previously_persisted_plaintext_keys_remain_readable_without_rewriting_rows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.EnsureCreatedAsync();
        }

        const string plaintext = "legacy-session-data";
        string legacyProtectedData;

        // Step 1: Legacy provider without certificate (persists plaintext XML key)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connection));
            services.AddDataProtection().SetApplicationName("ProphetOps");
            services.AddSingleton<PostgresXmlRepository>();
            services.AddOptions<KeyManagementOptions>().Configure<IServiceProvider>((options, sp) =>
                options.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>());

            using var legacySp = services.BuildServiceProvider();
            var protector = legacySp.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose");
            legacyProtectedData = protector.Protect(plaintext);
        }

        // Verify the legacy key was persisted as unencrypted with masterKey
        await using (var verifyDb = new AppDbContext(dbOptions))
        {
            var keys = await verifyDb.DataProtectionKeys.AsNoTracking().ToListAsync();
            Assert.Single(keys);
            var xml = keys[0].Xml;
            Assert.NotNull(xml);
            var doc = XDocument.Parse(xml);
            Assert.Contains(doc.Descendants(), e => e.Name.LocalName == "masterKey");
            Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "encryptedSecret");
            Assert.Contains("unencrypted form", xml, StringComparison.OrdinalIgnoreCase);
        }

        // Step 2: New provider configured with certificate encryption
        const string password = "TransitionPassword123!";
        var (base64, _, _) = CreateTestRsaCertificatePfx(password);
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Database:Provider", "postgres"),
            ("DataProtection:CertificateBase64", base64),
            ("DataProtection:CertificatePassword", password));

        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connection));
            var dp = services.AddDataProtection().SetApplicationName("ProphetOps");
            services.AddSingleton<PostgresXmlRepository>();
            services.AddOptions<KeyManagementOptions>().Configure<IServiceProvider>((options, sp) =>
                options.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>());
            DataProtectionConfiguration.Configure(dp, services, config, DatabaseProviderKind.Postgres, isHosted: true);

            using var modernSp = services.BuildServiceProvider();
            var protector = modernSp.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose");

            // Can still unprotect legacy data!
            var roundtrip = protector.Unprotect(legacyProtectedData);
            Assert.Equal(plaintext, roundtrip);
        }

        // Verify legacy row in DB was NOT rewritten or corrupted
        await using (var verifyDb = new AppDbContext(dbOptions))
        {
            var keys = await verifyDb.DataProtectionKeys.AsNoTracking().ToListAsync();
            Assert.Single(keys);
            var xml = keys[0].Xml;
            Assert.NotNull(xml);
            var doc = XDocument.Parse(xml);
            Assert.Contains(doc.Descendants(), e => e.Name.LocalName == "masterKey");
            Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "encryptedSecret");
        }
    }

    [Fact]
    public void Hosted_certificate_is_disposed_when_service_provider_is_disposed()
    {
        var (base64, password, _) = CreateTestRsaCertificatePfx();
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Database:Provider", "postgres"),
            ("DataProtection:CertificateBase64", base64),
            ("DataProtection:CertificatePassword", password));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite("Data Source=:memory:"));
        var dp = services.AddDataProtection().SetApplicationName("ProphetOps");
        services.AddSingleton<PostgresXmlRepository>();
        DataProtectionConfiguration.Configure(dp, services, config, DatabaseProviderKind.Postgres, isHosted: true);

        HostedDataProtectionCertificate holder;
        X509Certificate2 certInstance;
        using (var sp = services.BuildServiceProvider())
        {
            holder = sp.GetRequiredService<HostedDataProtectionCertificate>();
            certInstance = holder.Certificate;
            Assert.NotNull(certInstance);
            Assert.False(holder.IsDisposed);
            using (var rsaBefore = certInstance.GetRSAPrivateKey())
            {
                Assert.NotNull(rsaBefore);
            }

            var protector = sp.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");
            var encrypted = protector.Protect("hello");
            Assert.Equal("hello", protector.Unprotect(encrypted));
        }

        // After ServiceProvider is disposed, the DI-owned certificate is disposed
        Assert.True(holder.IsDisposed);
        var ex = Assert.ThrowsAny<Exception>(() => certInstance.GetRSAPrivateKey());
        Assert.True(ex is CryptographicException or ObjectDisposedException);
    }

    private static (string Base64, string Password, byte[] PfxBytes) CreateTestRsaCertificatePfx(
        string password = "TestPassword123!",
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        int keySize = 2048)
    {
        using var rsa = RSA.Create(keySize);
        var request = new CertificateRequest("CN=ProphetOps-Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var nb = notBefore ?? DateTimeOffset.UtcNow.AddDays(-1);
        var na = notAfter ?? DateTimeOffset.UtcNow.AddDays(30);
        using var cert = request.CreateSelfSigned(nb, na);
        var pfxBytes = cert.Export(X509ContentType.Pfx, password);
        return (Convert.ToBase64String(pfxBytes), password, pfxBytes);
    }

    private static (string Base64, string Password) CreateTestEcdsaCertificatePfx(string password = "TestPassword123!")
    {
        using var ecdsa = ECDsa.Create();
        var request = new CertificateRequest("CN=ProphetOps-Ecdsa-Test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        var pfxBytes = cert.Export(X509ContentType.Pfx, password);
        return (Convert.ToBase64String(pfxBytes), password);
    }

    private static (string Base64, string Password) CreateTestNoPrivateKeyPfx(string password = "TestPassword123!")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=ProphetOps-NoKey-Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        var certBytes = cert.Export(X509ContentType.Cert);
        using var pubCert = X509CertificateLoader.LoadCertificate(certBytes);
        var store = new X509Certificate2Collection(pubCert);
        var pfxBytes = store.Export(X509ContentType.Pfx, password);
        return (Convert.ToBase64String(pfxBytes!), password);
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

        var dp = services.AddDataProtection()
            .SetApplicationName("ProphetOps");
        services.AddSingleton<PostgresXmlRepository>();
        var configuredProvider = DataProtectionConfiguration.DetermineProvider(configuration);
        DataProtectionConfiguration.Configure(
            dp,
            services,
            configuration,
            configuredProvider,
            HostedRuntime.IsEnabled(configuration));
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
