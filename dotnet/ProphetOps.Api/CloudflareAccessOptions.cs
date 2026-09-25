namespace ProphetOps.Api;

public sealed class CloudflareAccessOptions
{
    public const string SectionName = "CloudflareAccess";
    public const string AssertionHeader = "Cf-Access-Jwt-Assertion";
    public const string ContextEmailKey = "CloudflareAccess.Email";
    public static readonly PathString HealthPath = new("/health/live");

    private CloudflareAccessOptions(bool enabled, string issuer, string audience, Uri jwksUrl)
    {
        Enabled = enabled;
        Issuer = issuer;
        Audience = audience;
        JwksUrl = jwksUrl;
    }

    public bool Enabled { get; }
    public string Issuer { get; }
    public string Audience { get; }
    public Uri JwksUrl { get; }
    public TimeSpan KeyCacheLifetime { get; } = TimeSpan.FromHours(6);

    public static CloudflareAccessOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var hosted = HostedRuntime.IsEnabled(configuration);
        var enabled = section.GetValue<bool>("Enabled");
        if (!hosted)
            return new CloudflareAccessOptions(enabled, "", "", new Uri("https://localhost/.well-known/cdn-cgi/access/certs"));

        if (!enabled)
            throw new InvalidOperationException("Hosted mode requires CloudflareAccess:Enabled=true.");

        var issuer = section["Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
            throw new InvalidOperationException("Hosted mode requires CloudflareAccess:Issuer.");

        var audience = section["Audience"];
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Hosted mode requires CloudflareAccess:Audience.");

        var jwks = section["JwksUrl"];
        if (!Uri.TryCreate(jwks, UriKind.Absolute, out var jwksUrl) || jwksUrl.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Hosted mode requires CloudflareAccess:JwksUrl to be an absolute HTTPS URL.");

        return new CloudflareAccessOptions(true, issuer.Trim(), audience.Trim(), jwksUrl);
    }
}
