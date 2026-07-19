using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProphetOps.Api;
using Xunit;

namespace ProphetOps.Api.Tests;

public class ImageUploadTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static MemoryStream Bytes(params byte[] head)
    {
        var buffer = new byte[Math.Max(head.Length, 12)];
        head.CopyTo(buffer, 0);
        return new MemoryStream(buffer);
    }

    [Fact]
    public void Recognises_a_jpeg_by_its_signature()
    {
        using var stream = Bytes(0xFF, 0xD8, 0xFF, 0xE0);
        Assert.Equal(".jpg", ImageUpload.SniffExtension(stream));
    }

    [Fact]
    public void Recognises_a_png_by_its_signature()
    {
        using var stream = Bytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
        Assert.Equal(".png", ImageUpload.SniffExtension(stream));
    }

    [Fact]
    public void Recognises_a_webp_by_its_signature()
    {
        using var stream = Bytes(0x52, 0x49, 0x46, 0x46, 0x20, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50);
        Assert.Equal(".webp", ImageUpload.SniffExtension(stream));
    }

    [Fact]
    public void Rejects_a_file_that_is_not_an_image_whatever_it_claims_to_be()
    {
        using var stream = Bytes(0x4D, 0x5A, 0x90, 0x00);
        Assert.Null(ImageUpload.SniffExtension(stream));
    }

    [Fact]
    public void Rejects_a_file_too_short_to_carry_a_signature()
    {
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8 });
        Assert.Null(ImageUpload.SniffExtension(stream));
    }

    [Fact]
    public void Leaves_the_stream_rewound_so_the_caller_can_still_save_it()
    {
        using var stream = Bytes(0xFF, 0xD8, 0xFF, 0xE0);
        ImageUpload.SniffExtension(stream);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Serves_only_the_three_formats_it_accepts()
    {
        Assert.Equal("image/jpeg", ImageUpload.ContentTypeFor("abc.jpg"));
        Assert.Equal("image/png", ImageUpload.ContentTypeFor("abc.png"));
        Assert.Equal("image/webp", ImageUpload.ContentTypeFor("abc.webp"));
        Assert.Null(ImageUpload.ContentTypeFor("abc.svg"));
        Assert.Null(ImageUpload.ContentTypeFor("abc.html"));
        Assert.Null(ImageUpload.ContentTypeFor("abc"));
    }

    [Fact]
    public void Names_stored_files_itself_so_a_caller_cannot_choose_the_path()
    {
        var first = ImageUpload.NewStoredName(".jpg");
        var second = ImageUpload.NewStoredName(".jpg");

        Assert.NotEqual(first, second);
        Assert.EndsWith(".jpg", first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('\\', first);
        Assert.DoesNotContain("..", first);
    }

    private async Task<HttpClient> SignedInOwner()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "owner@prophetops.local", password = "owner123" });
        login.EnsureSuccessStatusCode();

        var probe = await client.GetAsync("/api/auth/me");
        var cookie = probe.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN="));
        var token = Uri.UnescapeDataString(cookie.Split(';')[0]["XSRF-TOKEN=".Length..]);
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
        return client;
    }

    private static MultipartFormDataContent FileNamed(string name, string contentType, byte[] content)
    {
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { part, "file", name } };
    }

    [Fact]
    public async Task Upload_rejects_a_disguised_non_image()
    {
        var client = await SignedInOwner();

        // A Windows executable announced as a PNG. The name and content type are both the
        // caller's to choose, so neither may be what decides.
        var payload = new byte[64];
        payload[0] = 0x4D;
        payload[1] = 0x5A;

        var response = await client.PostAsync("/api/inventory/PKG-101/image",
            FileNamed("harmless.png", "image/png", payload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_stores_a_real_image_and_serves_it_back()
    {
        var client = await SignedInOwner();

        var png = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);

        var upload = await client.PostAsync("/api/inventory/PKG-101/image",
            FileNamed("photo.png", "image/png", png));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var listing = await client.GetFromJsonAsync<List<Dictionary<string, object?>>>("/api/inventory");
        var stored = listing!.Single(p => p["id"]?.ToString() == "PKG-101");
        Assert.NotNull(stored["imageUrl"]);

        var fetched = await client.GetAsync("/api/inventory/PKG-101/image");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal("image/png", fetched.Content.Headers.ContentType?.MediaType);

        var removed = await client.DeleteAsync("/api/inventory/PKG-101/image");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/inventory/PKG-101/image")).StatusCode);
    }

    [Fact]
    public async Task Upload_without_an_antiforgery_token_is_rejected()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "owner@prophetops.local", password = "owner123" });
        login.EnsureSuccessStatusCode();

        var png = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);

        var response = await client.PostAsync("/api/inventory/PKG-101/image",
            FileNamed("photo.png", "image/png", png));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
