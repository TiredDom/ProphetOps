using System.Net;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

/// An /api address with no endpoint behind it answers 404 in JSON instead of the app's page.
public class UnknownApiAddressTests
{
    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/api/bookings/extra/segments")]
    [InlineData("/api")]
    public async Task An_unknown_api_address_is_not_found_whether_signed_in_or_not(string path)
    {
        using var factory = new ApiFactory();
        using var anonymous = factory.CreateClient();
        using var signedIn = await AuthenticatedClient.Login(factory);

        foreach (var client in new[] { anonymous, signedIn })
        {
            var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("There is no API at this address.", body.GetProperty("message").GetString());
        }
    }

    [Fact]
    public async Task Real_api_endpoints_still_answer_as_before()
    {
        using var factory = new ApiFactory();
        using var anonymous = factory.CreateClient();
        using var signedIn = await AuthenticatedClient.Login(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/bookings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await signedIn.GetAsync("/api/bookings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await signedIn.GetAsync("/api/forecast")).StatusCode);
    }

    [Fact]
    public async Task A_page_address_outside_api_is_left_to_the_app()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/bookings");

        // The page is served from the built client when it exists; either way it is never the API's
        // JSON answer.
        Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
