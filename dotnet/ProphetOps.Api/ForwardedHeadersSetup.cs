using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ProphetOps.Api;

public static class ForwardedHeadersSetup
{
    public static ForwardedHeadersOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
        };

        foreach (var value in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        {
            if (!IPAddress.TryParse(value, out var address))
                throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contains an invalid IP address: {value}");
            options.KnownProxies.Add(address);
        }

        foreach (var value in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
        {
            var parts = value.Split('/', 2);
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var prefix) || !int.TryParse(parts[1], out var length))
                throw new InvalidOperationException($"ForwardedHeaders:KnownNetworks contains an invalid CIDR range: {value}");
            options.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, length));
        }

        return options;
    }

    public static bool HasTrustedBoundary(IConfiguration configuration) =>
        configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>()?.Length > 0
        || configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>()?.Length > 0;
}

public sealed record PublicTransportOptions(bool RequireSecureCookies)
{
    public static PublicTransportOptions FromConfiguration(IConfiguration configuration) =>
        new(HostedRuntime.IsEnabled(configuration)
            || configuration.GetValue("Transport:PublicHttps", false)
            || configuration.GetValue("PublicOrigin:Https", false));
}
