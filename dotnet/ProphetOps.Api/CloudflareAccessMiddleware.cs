using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ProphetOps.Api;

public sealed class CloudflareAccessMiddleware(RequestDelegate next)
{
    private static readonly JsonWebTokenHandler Handler = new();

    public async Task InvokeAsync(HttpContext context, CloudflareAccessOptions options, ICloudflareAccessKeyStore keys)
    {
        if (!HostedRuntime.IsEnabled(context.RequestServices.GetRequiredService<IConfiguration>()))
        {
            await next(context);
            return;
        }

        if (!options.Enabled)
        {
            await next(context);
            return;
        }

        if (context.Request.Path == CloudflareAccessOptions.HealthPath
            || context.Request.Path == CloudflareAccessOptions.ReadyPath)
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(CloudflareAccessOptions.AssertionHeader, out var values)
            || values.Count != 1
            || string.IsNullOrWhiteSpace(values[0]))
        {
            await Reject(context);
            return;
        }

        var result = await Validate(values[0]!, options, keys, context.RequestAborted);
        if (!result.Accepted)
        {
            await Reject(context);
            return;
        }

        context.Items[CloudflareAccessOptions.ContextEmailKey] = result.Email;
        await next(context);
    }

    private static async Task<AccessResult> Validate(
        string assertion,
        CloudflareAccessOptions options,
        ICloudflareAccessKeyStore keys,
        CancellationToken cancellationToken)
    {
        JsonWebToken token;
        try
        {
            token = Handler.ReadJsonWebToken(assertion);
        }
        catch (ArgumentException)
        {
            return AccessResult.Rejected;
        }

        if (!string.Equals(token.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))
            return AccessResult.Rejected;
        if (string.IsNullOrWhiteSpace(token.Kid))
            return AccessResult.Rejected;

        var key = await keys.FindKeyAsync(token.Kid, cancellationToken);
        if (key is null)
            return AccessResult.Rejected;

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = key,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "email",
            RoleClaimType = ClaimTypes.Role,
        };

        var validation = await Handler.ValidateTokenAsync(assertion, parameters);
        if (!validation.IsValid)
            return AccessResult.Rejected;

        var email = validation.ClaimsIdentity.FindFirst("email")?.Value;
        return string.IsNullOrWhiteSpace(email) ? AccessResult.Rejected : AccessResult.Accept(email);
    }

    private static async Task Reject(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { message = "Access assertion required." }, context.RequestAborted);
    }

    private sealed record AccessResult(bool Accepted, string? Email)
    {
        public static readonly AccessResult Rejected = new(false, null);
        public static AccessResult Accept(string email) => new(true, email);
    }
}

public sealed class CloudflareAccessSessionBindingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HostedRuntime.IsEnabled(context.RequestServices.GetRequiredService<IConfiguration>())
            || !context.RequestServices.GetRequiredService<CloudflareAccessOptions>().Enabled
            || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var accessEmail = context.Items[CloudflareAccessOptions.ContextEmailKey] as string;
        var appEmail = context.User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(accessEmail)
            || string.IsNullOrWhiteSpace(appEmail)
            || !string.Equals(accessEmail, appEmail, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Access identity does not match the application session." }, context.RequestAborted);
            return;
        }

        await next(context);
    }
}

public static class CloudflareAccessLogin
{
    public static bool MatchesAccessEmail(HttpContext context, string appEmail)
    {
        if (!HostedRuntime.IsEnabled(context.RequestServices.GetRequiredService<IConfiguration>()))
            return true;
        if (!context.RequestServices.GetRequiredService<CloudflareAccessOptions>().Enabled)
            return true;
        var accessEmail = context.Items[CloudflareAccessOptions.ContextEmailKey] as string;
        return !string.IsNullOrWhiteSpace(accessEmail)
            && string.Equals(accessEmail, appEmail, StringComparison.OrdinalIgnoreCase);
    }
}
