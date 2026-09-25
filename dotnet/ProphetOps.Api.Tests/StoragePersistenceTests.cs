using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Api;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class StoragePersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "prophetops-persistence-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Database_uploads_and_auth_cookie_survive_restart_with_same_storage_root()
    {
        string authCookie;
        var imageBytes = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(imageBytes, 0);

        using (var first = new PersistentFactory(_root))
        {
            using var client = first.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
            var login = await client.PostAsJsonAsync("/api/auth/login",
                new { email = "owner@prophetops.local", password = "owner123" });
            login.EnsureSuccessStatusCode();
            authCookie = login.Headers.GetValues("Set-Cookie").Single(header => header.StartsWith("prophetops=")).Split(';')[0];

            using var scope = first.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<StoragePaths>();
            Directory.CreateDirectory(storage.PackageImagesPath);
            await File.WriteAllBytesAsync(storage.UploadedPackageImage("persisted.png"), imageBytes);
            db.TravelPackages.Add(new TravelPackage
            {
                Code = "PERSIST",
                PackageName = "Persisted package",
                Destination = "Bohol",
                AvailableSlots = 4,
                ImagePath = "persisted.png",
            });
            db.SaveChanges();
            Assert.True(File.Exists(storage.DatabasePath));
            Assert.NotEmpty(Directory.GetFiles(storage.KeysPath));
        }

        using (var second = new PersistentFactory(_root))
        {
            using var client = second.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
            client.DefaultRequestHeaders.Add("Cookie", authCookie);

            var me = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);

            var image = await client.GetAsync("/api/inventory/PERSIST/image");
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
            Assert.Equal(imageBytes, await image.Content.ReadAsByteArrayAsync());

            using var scope = second.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(db.TravelPackages.AsNoTracking().Any(package => package.Code == "PERSIST"));
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class PersistentFactory(string root) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Enabled"] = "true",
                ["Storage:Root"] = root,
                ["Business:TimeZone"] = "Asia/Manila",
            }));
        }
    }
}
