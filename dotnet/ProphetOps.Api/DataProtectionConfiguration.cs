using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed class HostedDataProtectionCertificate : IDisposable
{
    private readonly X509Certificate2 _certificate;
    private bool _disposed;

    public bool IsDisposed => _disposed;
    public X509Certificate2 Certificate => _certificate;

    public HostedDataProtectionCertificate(X509Certificate2 certificate)
    {
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _certificate.Dispose();
        }
    }
}

public static class DataProtectionConfiguration
{
    public const string SectionName = "DataProtection";
    public const string CertificateBase64Key = "CertificateBase64";
    public const string CertificatePasswordKey = "CertificatePassword";

    public static X509Certificate2 LoadCertificate(string base64, string password)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException("DataProtection:CertificateBase64 is required.");

        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException("DataProtection:CertificatePassword is required.");

        byte[]? rawBytes = null;
        try
        {
            try
            {
                rawBytes = Convert.FromBase64String(base64.Trim());
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("DataProtection:CertificateBase64 is not a valid Base64 string.");
            }

            X509Certificate2 cert;
            try
            {
                cert = X509CertificateLoader.LoadPkcs12(rawBytes, password, X509KeyStorageFlags.EphemeralKeySet);
            }
            catch (Exception ex) when (ex is CryptographicException or InvalidDataException or ArgumentException)
            {
                throw new InvalidOperationException("Failed to load Data Protection certificate: invalid password or corrupted certificate archive.");
            }

            if (!cert.HasPrivateKey)
            {
                cert.Dispose();
                throw new InvalidOperationException("Data Protection certificate must contain a private key.");
            }

            using (var rsa = cert.GetRSAPrivateKey())
            {
                if (rsa is null)
                {
                    cert.Dispose();
                    throw new InvalidOperationException("Data Protection certificate must contain an RSA private key.");
                }

                if (rsa.KeySize < 2048)
                {
                    cert.Dispose();
                    throw new InvalidOperationException("Data Protection certificate RSA key size must be at least 2048 bits.");
                }
            }

            var now = DateTimeOffset.UtcNow;
            if (now < cert.NotBefore.ToUniversalTime())
            {
                cert.Dispose();
                throw new InvalidOperationException("Data Protection certificate is not yet valid.");
            }

            if (now > cert.NotAfter.ToUniversalTime())
            {
                cert.Dispose();
                throw new InvalidOperationException("Data Protection certificate has expired.");
            }

            return cert;
        }
        finally
        {
            if (rawBytes is not null)
            {
                CryptographicOperations.ZeroMemory(rawBytes);
            }
        }
    }

    public static X509Certificate2? ResolveCertificate(
        IConfiguration configuration,
        DatabaseProviderKind provider,
        bool isHosted)
    {
        if (provider != DatabaseProviderKind.Postgres)
            return null;

        var section = configuration.GetSection(SectionName);
        var base64 = section[CertificateBase64Key];
        var password = section[CertificatePasswordKey];

        if (isHosted)
        {
            if (string.IsNullOrWhiteSpace(base64))
                throw new InvalidOperationException("Hosted PostgreSQL mode requires DataProtection:CertificateBase64.");
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Hosted PostgreSQL mode requires DataProtection:CertificatePassword.");
            return LoadCertificate(base64, password);
        }

        if (!string.IsNullOrWhiteSpace(base64))
        {
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("DataProtection:CertificatePassword is required when DataProtection:CertificateBase64 is configured.");
            return LoadCertificate(base64, password);
        }

        return null;
    }

    public static void Configure(
        IDataProtectionBuilder builder,
        IServiceCollection services,
        IConfiguration configuration,
        DatabaseProviderKind provider,
        bool isHosted,
        bool isOfflineCommand = false)
    {
        if (isOfflineCommand)
            return;

        var cert = ResolveCertificate(configuration, provider, isHosted);
        if (cert is not null)
        {
            builder.ProtectKeysWithCertificate(cert);
            services.AddSingleton(sp => new HostedDataProtectionCertificate(cert));
            builder.Services.AddOptions<KeyManagementOptions>().Configure<IServiceProvider>((options, sp) =>
            {
                _ = sp.GetService<HostedDataProtectionCertificate>();
            });
        }
    }

    public static DatabaseProviderKind DetermineProvider(IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"]?.Trim();
        if (string.Equals(provider, "postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "postgresql", StringComparison.OrdinalIgnoreCase))
            return DatabaseProviderKind.Postgres;
        return DatabaseProviderKind.Sqlite;
    }
}
