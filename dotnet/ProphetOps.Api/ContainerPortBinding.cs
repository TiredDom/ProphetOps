using Microsoft.AspNetCore.Builder;

namespace ProphetOps.Api;

public static class ContainerPortBinding
{
    public static string? BuildUrl(string? port, string? configuredUrls = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredUrls)) return null;
        if (string.IsNullOrWhiteSpace(port)) return null;

        if (!int.TryParse(port, out var portNumber) || portNumber is < 1 or > 65535)
            throw new InvalidOperationException("PORT must be a TCP port number from 1 through 65535.");

        return $"http://0.0.0.0:{portNumber}";
    }

    public static void Configure(WebApplicationBuilder builder, string[] args)
    {
        var url = BuildUrl(builder.Configuration["PORT"], FindExplicitUrls(args));
        if (url is not null) builder.WebHost.UseUrls(url);
    }

    private static string? FindExplicitUrls(string[] args)
    {
        var environmentUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
            ?? Environment.GetEnvironmentVariable("DOTNET_URLS");
        if (!string.IsNullOrWhiteSpace(environmentUrls)) return environmentUrls;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg.Equals("--urls", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                return args[index + 1];
            if (arg.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase))
                return arg["--urls=".Length..];
            if (arg.StartsWith("urls=", StringComparison.OrdinalIgnoreCase))
                return arg["urls=".Length..];
        }

        return null;
    }
}
