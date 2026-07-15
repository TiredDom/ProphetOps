using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class SecurityTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Responses_carry_hardened_security_headers()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("DENY", string.Join("", response.Headers.GetValues("X-Frame-Options")));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.Contains("default-src 'self'", string.Join("", response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task Protected_endpoint_requires_authentication()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_is_exempt_from_antiforgery()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "owner@prophetops.local", password = "owner123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Write_without_antiforgery_token_is_rejected()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "owner@prophetops.local", password = "owner123" });
        login.EnsureSuccessStatusCode();

        var write = await client.PostAsJsonAsync("/api/bookings", new
        {
            id = "BKG-CSRF1",
            ds = "2026-07-10",
            y = 2,
            client = "No Token",
            package = "Ad hoc",
            destination = "Cebu",
            grossRevenue = 20000,
            paymentStatus = "Pending",
            bookingStatus = "Pending",
            entryType = "Manual quotation",
            source = "Manual quotation",
        });

        Assert.Equal(HttpStatusCode.BadRequest, write.StatusCode);
    }

    [Fact]
    public async Task Write_with_antiforgery_token_succeeds()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "owner@prophetops.local", password = "owner123" });
        login.EnsureSuccessStatusCode();

        var probe = await client.GetAsync("/api/auth/me");
        var cookie = probe.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN="));
        var token = Uri.UnescapeDataString(cookie.Split(';')[0]["XSRF-TOKEN=".Length..]);
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);

        var write = await client.PostAsJsonAsync("/api/bookings", new
        {
            id = "BKG-CSRF2",
            ds = "2026-07-10",
            y = 2,
            client = "With Token",
            package = "Ad hoc",
            destination = "Cebu",
            grossRevenue = 20000,
            paymentStatus = "Pending",
            bookingStatus = "Pending",
            entryType = "Manual quotation",
            source = "Manual quotation",
        });

        Assert.Equal(HttpStatusCode.OK, write.StatusCode);
    }
}
