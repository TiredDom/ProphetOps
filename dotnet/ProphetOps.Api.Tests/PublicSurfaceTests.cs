using System.Net;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

/// What a visitor who has not signed in can learn: nothing about whose system this is, and every
/// answer asks search engines to leave the site out of their results.
public class PublicSurfaceTests
{
    [Fact]
    public async Task The_business_settings_need_a_signed_in_user_and_carry_the_agency_name()
    {
        using var factory = new ApiFactory();
        using var anonymous = factory.CreateClient();
        using var signedIn = await AuthenticatedClient.Login(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/app/config")).StatusCode);

        var response = await signedIn.GetAsync("/api/app/config");
        response.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Asia/Manila", body.GetProperty("timeZone").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", body.GetProperty("today").GetString());
        Assert.Equal("Renan-Tina Travels & Tours", body.GetProperty("businessName").GetString());
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/api/auth/me")]
    [InlineData("/api/app/config")]
    [InlineData("/api/does-not-exist")]
    [InlineData("/health/live")]
    public async Task Every_answer_asks_search_engines_not_to_list_the_site(string path)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.True(response.Headers.TryGetValues("X-Robots-Tag", out var values), $"No X-Robots-Tag on {path}");
        Assert.Equal("noindex, nofollow", Assert.Single(values));
    }
}
