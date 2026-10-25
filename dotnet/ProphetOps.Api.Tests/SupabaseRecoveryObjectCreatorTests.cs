using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class SupabaseRecoveryObjectCreatorTests : IDisposable
{
    private readonly string _testId = Guid.NewGuid().ToString("N");

    [Fact]
    public async Task TryCreateAsync_successful_create_returns_true()
    {
        var handler = new FakeHttpHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("https://testref.supabase.co/storage/v1/object/test-bucket/rec/packages/image1.png", req.RequestUri?.AbsoluteUri);
            Assert.Equal("false", req.Headers.GetValues("x-upsert").Single());
            Assert.Equal("sb_secret_valid123", req.Headers.GetValues("apikey").Single());
            Assert.Null(req.Headers.Authorization); // Modern sb_secret key must NOT set Authorization Bearer
            Assert.Equal("image/png", req.Content?.Headers.ContentType?.MediaType);
            Assert.Equal(5, req.Content?.Headers.ContentLength);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"Key\":\"test-bucket/rec/packages/image1.png\"}", Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_valid123", httpClient: client);

        using var ms = new MemoryStream([1, 2, 3, 4, 5]);
        var result = await creator.TryCreateAsync("packages/image1.png", ms, "image/png", 5, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TryCreateAsync_legacy_jwt_sets_authorization_bearer()
    {
        const string jwtKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJyb2xlIjoic2VydmljZV9yb2xlIn0.c2lnbmF0dXJl";
        var handler = new FakeHttpHandler(req =>
        {
            Assert.Equal(jwtKey, req.Headers.GetValues("apikey").Single());
            Assert.NotNull(req.Headers.Authorization);
            Assert.Equal("Bearer", req.Headers.Authorization.Scheme);
            Assert.Equal(jwtKey, req.Headers.Authorization.Parameter);

            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: jwtKey, httpClient: client);

        using var ms = new MemoryStream([1, 2, 3]);
        var result = await creator.TryCreateAsync("packages/test.png", ms, "image/png", 3, CancellationToken.None);

        Assert.True(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "{\"statusCode\": 409, \"error\": \"Duplicate\", \"message\": \"The resource already exists\"}")]
    [InlineData(HttpStatusCode.Conflict, "{\"code\": \"ResourceAlreadyExists\", \"message\": \"The resource already exists\"}")]
    [InlineData(HttpStatusCode.Conflict, "{\"code\": \"KeyAlreadyExists\"}")]
    [InlineData(HttpStatusCode.Conflict, "{\"code\": \"already_exists\"}")]
    [InlineData(HttpStatusCode.Conflict, "{\"error\": \"already_exists\"}")]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\": \"Duplicate\", \"message\": \"The resource already exists\"}")]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\": \"already_exists\"}")]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\": \"already_exists\"}")]
    public async Task TryCreateAsync_documented_duplicate_responses_return_false(HttpStatusCode status, string responseJson)
    {
        var handler = new FakeHttpHandler(req => new HttpResponseMessage(status)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);

        using var ms = new MemoryStream([1, 2, 3]);
        var result = await creator.TryCreateAsync("packages/existing.png", ms, "image/png", 3, CancellationToken.None);

        Assert.False(result); // Validated existing object detected
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\": \"InvalidInput\"}")]
    [InlineData(HttpStatusCode.Conflict, "{\"error\": \"SomeOtherConflict\"}")]
    [InlineData(HttpStatusCode.Unauthorized, "{\"message\": \"Invalid API key\"}")]
    [InlineData(HttpStatusCode.Forbidden, "{\"message\": \"Access denied\"}")]
    [InlineData(HttpStatusCode.NotFound, "{\"message\": \"Bucket not found\"}")]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"message\": \"Rate limit exceeded\"}")]
    [InlineData(HttpStatusCode.InternalServerError, "{\"message\": \"Internal server error\"}")]
    public async Task TryCreateAsync_arbitrary_errors_throw_without_blanket_duplicate_mapping(HttpStatusCode status, string errorJson)
    {
        var handler = new FakeHttpHandler(req => new HttpResponseMessage(status)
        {
            Content = new StringContent(errorJson, Encoding.UTF8, "application/json")
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);

        using var ms = new MemoryStream([1, 2, 3]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creator.TryCreateAsync("packages/fail.png", ms, "image/png", 3, CancellationToken.None));

        Assert.Contains(((int)status).ToString(), ex.Message);
        Assert.DoesNotContain(errorJson, ex.Message); // Never leak response body
    }

    [Fact]
    public async Task TryCreateAsync_rejects_http_redirects()
    {
        var handler = new FakeHttpHandler(req => new HttpResponseMessage(HttpStatusCode.MovedPermanently)
        {
            Headers = { Location = new Uri("https://evil.target.com/storage/v1/object/test-bucket/rec/key") }
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);

        using var ms = new MemoryStream([1, 2, 3]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creator.TryCreateAsync("packages/redirect.png", ms, "image/png", 3, CancellationToken.None));

        Assert.Contains("Redirects are prohibited", ex.Message);
    }

    [Fact]
    public async Task TryCreateAsync_handles_oversized_error_response_safely()
    {
        var bigBody = "{\"error\": \"Other\", \"pad\": \"" + new string('A', 100_000) + "\"}";
        var handler = new FakeHttpHandler(req => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(bigBody, Encoding.UTF8, "application/json")
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);

        using var ms = new MemoryStream([1, 2, 3]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creator.TryCreateAsync("packages/bigerror.png", ms, "image/png", 3, CancellationToken.None));

        Assert.Contains("400", ex.Message);
        Assert.DoesNotContain("pad", ex.Message);
    }

    [Fact]
    public async Task TryCreateAsync_handles_malformed_error_response_safely()
    {
        var handler = new FakeHttpHandler(req => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("<!DOCTYPE html><html><body>Error</body></html>", Encoding.UTF8, "text/html")
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);

        using var ms = new MemoryStream([1, 2, 3]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creator.TryCreateAsync("packages/malformed.png", ms, "image/png", 3, CancellationToken.None));

        Assert.Contains("409", ex.Message);
    }

    [Fact]
    public async Task TryCreateAsync_cancellation_throws_operation_canceled_exception()
    {
        var handler = new FakeHttpHandler(req =>
        {
            throw new TaskCanceledException();
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var ms = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            creator.TryCreateAsync("packages/canceled.png", ms, "image/png", 3, cts.Token));
    }

    [Theory]
    [InlineData("https://otherref.supabase.co", "Recovery project URL ref 'otherref' does not match S3 endpoint ref 'testref'.")]
    [InlineData("http://testref.supabase.co", "ObjectStorage:Recovery:SupabaseProjectUrl must be a valid HTTPS URL.")]
    [InlineData("https://user:pass@testref.supabase.co", "ObjectStorage:Recovery:SupabaseProjectUrl must not contain user credentials, queries, or fragments.")]
    [InlineData("https://testref.supabase.co?query=1", "ObjectStorage:Recovery:SupabaseProjectUrl must not contain user credentials, queries, or fragments.")]
    [InlineData("https://testref.supabase.co#fragment", "ObjectStorage:Recovery:SupabaseProjectUrl must not contain user credentials, queries, or fragments.")]
    public void Constructor_rejects_invalid_or_mismatched_project_url(string projectUrl, string expectedError)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = projectUrl,
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test"));

        Assert.Contains(expectedError, ex.Message);
    }

    [Fact]
    public void Constructor_rejects_missing_api_key()
    {
        var config = CreateValidConfig();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new SupabaseRecoveryObjectCreator(config, recoveryApiKey: ""));

        Assert.Contains("ObjectStorage__Recovery__SupabaseApiKey environment variable is required", ex.Message);
    }

    [Fact]
    public async Task TryCreateAsync_direct_storage_s3_hostname_matches_api_hostname()
    {
        var handler = new FakeHttpHandler(req =>
        {
            Assert.Equal("https://testref.supabase.co/storage/v1/object/test-bucket/rec/packages/image1.png", req.RequestUri?.AbsoluteUri);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var client = new HttpClient(handler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref.storage.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:S3:RecoveryPrefix"] = "rec/",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = "https://testref.supabase.co",
            })
            .Build();

        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);
        using var ms = new MemoryStream([1, 2, 3]);
        var result = await creator.TryCreateAsync("packages/image1.png", ms, "image/png", 3, CancellationToken.None);

        Assert.True(result);
    }

    [Theory]
    [InlineData("https://testref.supabase.co.evil.com")]
    [InlineData("https://testref-storage.supabase.co")]
    [InlineData("https://evil.supabase.co")]
    public void Constructor_rejects_deceptive_or_mismatched_project_suffixes(string badUrl)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref.storage.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = badUrl,
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test"));
    }

    [Theory]
    [InlineData("https://testref.supabase.co:8443")]
    [InlineData("https://testref.storage.supabase.co:9000/storage/v1/s3")]
    public void Constructor_rejects_nonstandard_ports(string endpointWithPort)
    {
        var isS3 = endpointWithPort.Contains("storage.supabase.co");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = isS3 ? endpointWithPort : "https://testref.storage.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = isS3 ? "https://testref.supabase.co" : endpointWithPort,
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test"));
    }

    [Theory]
    [InlineData("https://testref.supabase.co/extra/path")]
    [InlineData("https://testref.supabase.co/v1")]
    public void Constructor_rejects_unexpected_path_on_project_url(string projectUrlWithPath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref.storage.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = projectUrlWithPath,
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test"));
    }

    [Theory]
    [InlineData("https://testref.storage.supabase.co/")]
    [InlineData("https://testref.storage.supabase.co/wrong/path")]
    public void Constructor_rejects_unexpected_path_on_s3_url(string s3UrlWithBadPath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = s3UrlWithBadPath,
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = "https://testref.supabase.co",
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test"));
    }

    [Fact]
    public async Task TryCreateAsync_stalled_body_read_obeys_per_request_timeout()
    {
        using var fixtureCleanupCts = new CancellationTokenSource();
        var handler = new FakeHttpHandler(req =>
        {
            var slowStream = new StallingStream(fixtureCleanupCts);
            var response = new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StreamContent(slowStream)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(
            config,
            recoveryApiKey: "sb_secret_test",
            httpClient: client,
            perRequestTimeout: TimeSpan.FromMilliseconds(50));

        using var ms = new MemoryStream([1, 2, 3]);

        // Independent bounded watchdog: fails the test if TryCreateAsync hangs (e.g. pre-fix helper)
        var watchdogTimeout = TimeSpan.FromSeconds(2);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var task = creator.TryCreateAsync("packages/stalled.png", ms, "image/png", 3, CancellationToken.None);
            var completedTask = await Task.WhenAny(task, Task.Delay(watchdogTimeout));
            if (completedTask != task)
            {
                Assert.Fail($"Pre-fix regression hung: stalled body read did not obey per-request timeout within watchdog limit of {watchdogTimeout.TotalMilliseconds}ms.");
            }

            await Assert.ThrowsAnyAsync<Exception>(() => task);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 1500, $"Stalled body read should fail quickly on per-request timeout (took {sw.ElapsedMilliseconds}ms).");
        }
        finally
        {
            fixtureCleanupCts.Cancel();
        }
    }

    [Fact]
    public async Task TryCreateAsync_stalled_send_obeys_per_request_timeout()
    {
        using var fixtureCleanupCts = new CancellationTokenSource();
        var handler = new FakeAsyncHttpHandler(async (req, ct) =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, fixtureCleanupCts.Token);
            await Task.Delay(Timeout.Infinite, linked.Token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(
            config,
            recoveryApiKey: "sb_secret_test",
            httpClient: client,
            perRequestTimeout: TimeSpan.FromMilliseconds(50));

        using var ms = new MemoryStream([1, 2, 3]);

        var watchdogTimeout = TimeSpan.FromSeconds(2);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var task = creator.TryCreateAsync("packages/stalled-send.png", ms, "image/png", 3, CancellationToken.None);
            var completedTask = await Task.WhenAny(task, Task.Delay(watchdogTimeout));
            if (completedTask != task)
            {
                Assert.Fail($"Pre-fix regression hung: stalled send did not obey per-request timeout within watchdog limit of {watchdogTimeout.TotalMilliseconds}ms.");
            }

            await Assert.ThrowsAnyAsync<Exception>(() => task);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 1500, $"Stalled send should fail quickly on per-request timeout (took {sw.ElapsedMilliseconds}ms).");
        }
        finally
        {
            fixtureCleanupCts.Cancel();
        }
    }

    [Fact]
    public async Task TryCreateAsync_exact_limit_response_is_parsed_and_over_limit_response_fails_closed()
    {
        var validDupJson = "{\"code\":\"already_exists\"}";
        var exactLimitJson = validDupJson + new string(' ', 65536 - validDupJson.Length);
        Assert.Equal(65536, exactLimitJson.Length);

        var handlerExact = new FakeHttpHandler(req => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(exactLimitJson, Encoding.UTF8, "application/json")
        });

        using var clientExact = new HttpClient(handlerExact);
        var config = CreateValidConfig();
        using var creatorExact = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: clientExact);
        using var ms1 = new MemoryStream([1, 2, 3]);
        var resExact = await creatorExact.TryCreateAsync("packages/exact.png", ms1, "image/png", 3, CancellationToken.None);
        Assert.False(resExact); // Exact limit is parsed as valid duplicate

        var overLimitJson = validDupJson + new string(' ', 65537 - validDupJson.Length);
        Assert.Equal(65537, overLimitJson.Length);

        var handlerOver = new FakeHttpHandler(req => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(overLimitJson, Encoding.UTF8, "application/json")
        });

        using var clientOver = new HttpClient(handlerOver);
        using var creatorOver = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: clientOver);
        using var ms2 = new MemoryStream([1, 2, 3]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creatorOver.TryCreateAsync("packages/over.png", ms2, "image/png", 3, CancellationToken.None));

        Assert.Contains("exceeded maximum allowed size", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "{\"code\":\"LockTimeout\",\"message\":\"The resource already exists\"}")]
    [InlineData(HttpStatusCode.Conflict, "{\"error\":\"BucketLocked\",\"message\":\"already_exists\"}")]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":\"InvalidInput\",\"message\":\"The resource already exists\"}")]
    public async Task TryCreateAsync_conflicting_code_and_message_fails_closed(HttpStatusCode status, string errorJson)
    {
        var handler = new FakeHttpHandler(req => new HttpResponseMessage(status)
        {
            Content = new StringContent(errorJson, Encoding.UTF8, "application/json")
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);

        using var ms = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creator.TryCreateAsync("packages/conflict.png", ms, "image/png", 3, CancellationToken.None));
    }

    [Fact]
    public async Task TryCreateAsync_duplicate_or_case_aliased_json_properties_fails_closed()
    {
        var dupJson = "{\"code\":\"already_exists\",\"Code\":\"already_exists\"}";
        var handler = new FakeHttpHandler(req => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(dupJson, Encoding.UTF8, "application/json")
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);

        using var ms = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creator.TryCreateAsync("packages/dup-json.png", ms, "image/png", 3, CancellationToken.None));
    }

    [Fact]
    public async Task TryCreateAsync_redacts_transport_exceptions_and_removes_inner_exception_chain()
    {
        const string secretKey = "SECRET_TRANSPORT_KEY_12345";
        var handler = new FakeHttpHandler(req =>
        {
            throw new HttpRequestException($"Failed to connect with secret={secretKey}");
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);

        using var ms = new MemoryStream([1, 2, 3]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creator.TryCreateAsync("packages/secret-leak.png", ms, "image/png", 3, CancellationToken.None));

        Assert.Null(ex.InnerException);
        Assert.DoesNotContain(secretKey, ex.Message);
        Assert.DoesNotContain(secretKey, ex.ToString());
    }

    [Fact]
    public async Task TryCreateAsync_redacts_mid_body_read_exceptions_and_removes_inner_exception_chain()
    {
        const string secret = "STREAM_SECRET_LEAK_999";
        var handler = new FakeHttpHandler(req =>
        {
            var throwingStream = new ThrowingStream(secret);
            var response = new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StreamContent(throwingStream)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        });

        using var client = new HttpClient(handler);
        var config = CreateValidConfig();
        using var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test", httpClient: client);

        using var ms = new MemoryStream([1, 2, 3]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creator.TryCreateAsync("packages/midstream.png", ms, "image/png", 3, CancellationToken.None));

        Assert.Null(ex.InnerException);
        Assert.DoesNotContain(secret, ex.Message);
        Assert.DoesNotContain(secret, ex.ToString());
    }

    [Theory]
    [InlineData("sb_pub_1234567890", "Publishable")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJyb2xlIjoiYW5vbiJ9.c2lnbmF0dXJl", "service_role")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJyb2xlIjoiYXV0aGVudGljYXRlZCJ9.c2lnbmF0dXJl", "service_role")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.notjson.c2lnbmF0dXJl", "JWT")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", "JWT")]
    [InlineData("sb_secret_key\r\ninjected: header", "newline")]
    public void Constructor_credential_branch_validation(string badKey, string expectedErrorSubstr)
    {
        var config = CreateValidConfig();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new SupabaseRecoveryObjectCreator(config, recoveryApiKey: badKey));

        Assert.Contains(expectedErrorSubstr, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispose_cleans_up_owned_http_client()
    {
        var config = CreateValidConfig();
        var creator = new SupabaseRecoveryObjectCreator(config, recoveryApiKey: "sb_secret_test");
        creator.Dispose();
        // Disposing twice is safe and does not throw
        creator.Dispose();
    }

    private static IConfiguration CreateValidConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:S3:RecoveryPrefix"] = "rec/",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = "https://testref.supabase.co",
            })
            .Build();
    }

    private sealed class StallingStream(CancellationTokenSource fixtureCleanupCts) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, fixtureCleanupCts.Token);
            await Task.Delay(Timeout.Infinite, linked.Token);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ThrowingStream(string secret) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException($"Stream read error with secret={secret}");
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new IOException($"Stream read error with secret={secret}");
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(handler(request));
        }
    }

    private sealed class FakeAsyncHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }

    public void Dispose() { }
}
