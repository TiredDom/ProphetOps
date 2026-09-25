using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class SessionRevocationTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    [Theory]
    [InlineData("/api/dashboard")]
    [InlineData("/api/auth/me")]
    [InlineData("/api/export/bookings.csv")]
    [InlineData("/api/inventory/PKG-101/image")]
    public async Task Suspending_an_account_rejects_its_existing_cookie(string path)
    {
        using var admin = await AuthenticatedClient.Login(_factory, "admin@prophetops.local", "admin123");
        using var owner = await AuthenticatedClient.Login(_factory);
        var update = await owner.PutAsJsonAsync("/api/users/admin@prophetops.local", Update(status: "Suspended"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Reactivating_an_account_does_not_reactivate_its_old_cookie()
    {
        using var admin = await AuthenticatedClient.Login(_factory, "admin@prophetops.local", "admin123");
        using var owner = await AuthenticatedClient.Login(_factory);
        (await owner.PutAsJsonAsync("/api/users/admin@prophetops.local", Update(status: "Suspended"))).EnsureSuccessStatusCode();
        (await owner.PutAsJsonAsync("/api/users/admin@prophetops.local", Update())).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/auth/me")).StatusCode);
        using var fresh = await AuthenticatedClient.Login(_factory, "admin@prophetops.local", "admin123");
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/dashboard")).StatusCode);
    }

    [Theory]
    [InlineData("Staff", "")]
    [InlineData("Admin", "replacement-test-password")]
    public async Task Changing_role_or_password_rejects_the_old_cookie(string role, string password)
    {
        using var admin = await AuthenticatedClient.Login(_factory, "admin@prophetops.local", "admin123");
        using var owner = await AuthenticatedClient.Login(_factory);
        (await owner.PutAsJsonAsync("/api/users/admin@prophetops.local", Update(role: role, password: password))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Removed_account_cannot_continue_with_its_cookie()
    {
        using var admin = await AuthenticatedClient.Login(_factory, "admin@prophetops.local", "admin123");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Remove(db.Users.Single(u => u.Email == "admin@prophetops.local"));
            db.SaveChanges();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Unchanged_active_session_survives_a_name_only_edit()
    {
        using var admin = await AuthenticatedClient.Login(_factory, "admin@prophetops.local", "admin123");
        using var owner = await AuthenticatedClient.Login(_factory);
        (await owner.PutAsJsonAsync("/api/users/admin@prophetops.local", Update())).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Cookie_issued_before_session_versioning_requires_a_new_login()
    {
        using var owner = await AuthenticatedClient.Login(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = db.Users.Single(u => u.Email == "admin@prophetops.local");
        var scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.Name), new Claim(ClaimTypes.Email, account.Email),
            new Claim(ClaimTypes.Role, account.Role),
        }, scheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        }, scheme);
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
        using var legacy = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        legacy.DefaultRequestHeaders.Add("Cookie", "prophetops=" + options.TicketDataFormat.Protect(ticket));
        Assert.Equal(HttpStatusCode.Unauthorized, (await legacy.GetAsync("/api/auth/me")).StatusCode);
    }

    private static object Update(string role = Roles.Admin, string status = "Active", string password = "") => new
    {
        name = "Updated admin", email = "admin@prophetops.local", role, status, password,
    };

    public void Dispose() => _factory.Dispose();
}
