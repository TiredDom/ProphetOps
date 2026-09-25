using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProphetOps.Api.Tests;

internal static class AuthenticatedClient
{
    public static async Task<HttpClient> Login(WebApplicationFactory<Program> factory,
        string email = "owner@prophetops.local", string password = "owner123")
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        await ArmAntiforgery(client);
        return client;
    }

    public static async Task ArmAntiforgery(HttpClient client)
    {
        var probe = await client.GetAsync("/api/auth/me");
        probe.EnsureSuccessStatusCode();
        var cookie = probe.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN="));
        var value = cookie.Split(';')[0]["XSRF-TOKEN=".Length..];
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(value));
    }
}
