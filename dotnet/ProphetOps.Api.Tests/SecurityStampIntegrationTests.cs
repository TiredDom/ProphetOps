using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProphetOps.Api;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class SecurityStampIntegrationTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "prophetops-secstamp-tests-" + Guid.NewGuid().ToString("N"));

    public SecurityStampIntegrationTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    /// <summary>
    /// Direct cookie-validation disaster simulation. Simulates a point-in-time database snapshot
    /// and subsequent restore with manual SQL stamp rotation.
    /// NOTE: This is a cookie-validation simulation, not end-to-end backup package recovery
    /// (which is exercised by OperatorRestoreScriptTests and in required CI with real writer/script preserved-key cookies).
    /// </summary>
    [Fact]
    public async Task Restore_invalidates_all_pre_backup_and_post_backup_unrestored_cookies_while_preserving_dataprotection_keys()
    {
        var dbPath = Path.Combine(_testRoot, "prophetops.db");
        var backupPath = Path.Combine(_testRoot, "prophetops.backup.db");
        var keysDir = Path.Combine(_testRoot, "keys");
        Directory.CreateDirectory(keysDir);

        using (var factory = new PersistentStorageApiFactory(_testRoot, dbPath))
        {
            // Seed database
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                db.Users.Add(new User
                {
                    Name = "Owner",
                    Email = "owner@prophetops.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
                    Role = Roles.OwnerManagement,
                    Status = "Active",
                    SessionVersion = 1,
                    SecurityStamp = Guid.NewGuid(),
                });
                await db.SaveChangesAsync();
            }

            // 1. Pre-backup login
            var preBackupClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var preLogin = await preBackupClient.PostAsJsonAsync("/api/auth/login", new { email = "owner@prophetops.local", password = "owner123" });
            preLogin.EnsureSuccessStatusCode();

            var preMe = await preBackupClient.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, preMe.StatusCode);

            // Extract pre-backup auth cookie header
            var preCookies = preLogin.Headers.GetValues("Set-Cookie").ToList();
            var authCookieHeader = preCookies.First(c => c.StartsWith("prophetops=")).Split(';')[0];

            // 2. Snapshot the database (representing backup capture)
            SqliteConnection.ClearAllPools();
            File.Copy(dbPath, backupPath, overwrite: true);

            // 3. Post-backup mutation: User updates password on live system
            await AuthenticatedClient.ArmAntiforgery(preBackupClient);
            var updateResp = await preBackupClient.PutAsJsonAsync("/api/users/owner@prophetops.local", new
            {
                name = "Owner",
                email = "owner@prophetops.local",
                role = Roles.OwnerManagement,
                status = "Active",
                password = "newpassword123",
            });
            updateResp.EnsureSuccessStatusCode();

            // Pre-backup cookie is now invalidated on the live system
            var preMeAfterMutation = await preBackupClient.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, preMeAfterMutation.StatusCode);

            // Login with new password -> Post-backup mutation cookie
            var postBackupClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var postLogin = await postBackupClient.PostAsJsonAsync("/api/auth/login", new { email = "owner@prophetops.local", password = "newpassword123" });
            postLogin.EnsureSuccessStatusCode();
            var postMe = await postBackupClient.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, postMe.StatusCode);

            var postCookies = postLogin.Headers.GetValues("Set-Cookie").ToList();
            var postAuthCookieHeader = postCookies.First(c => c.StartsWith("prophetops=")).Split(';')[0];

            // 4. Disaster & Restore: Restore from historical backup + execute stamp rotation
            SqliteConnection.ClearAllPools();
            File.Copy(backupPath, dbPath, overwrite: true);

            // Rotate SecurityStamp as restore-backup.ps1 does
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                await connection.OpenAsync();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE Users SET SecurityStamp = lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6)));";
                await cmd.ExecuteNonQueryAsync();
            }
            SqliteConnection.ClearAllPools();

            // 5. Assertions against restored database (using the EXACT same DataProtection keys)
            // (a) Pre-backup cookie: REJECTED
            var testClientPre = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
            testClientPre.DefaultRequestHeaders.Add("Cookie", authCookieHeader);
            var checkPre = await testClientPre.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, checkPre.StatusCode);

            // (b) Post-backup mutation cookie: REJECTED
            var testClientPost = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
            testClientPost.DefaultRequestHeaders.Add("Cookie", postAuthCookieHeader);
            var checkPost = await testClientPost.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, checkPost.StatusCode);

            // (c) Fresh login against restored database (restored password 'owner123'): ACCEPTED
            var newLoginClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            var freshLogin = await newLoginClient.PostAsJsonAsync("/api/auth/login", new { email = "owner@prophetops.local", password = "owner123" });
            freshLogin.EnsureSuccessStatusCode();
            var freshMe = await newLoginClient.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, freshMe.StatusCode);
        }
    }

    [Theory]
    [InlineData("missing", HttpStatusCode.Unauthorized)]
    [InlineData("empty_string", HttpStatusCode.Unauthorized)]
    [InlineData("guid_empty", HttpStatusCode.Unauthorized)]
    [InlineData("malformed", HttpStatusCode.Unauthorized)]
    [InlineData("wrong_guid", HttpStatusCode.Unauthorized)]
    [InlineData("matching_guid", HttpStatusCode.OK)]
    public async Task Security_stamp_claims_are_strictly_validated(string stampCase, HttpStatusCode expectedStatus)
    {
        var dbPath = Path.Combine(_testRoot, $"stamp_eval_{stampCase}.db");
        using var factory = new PersistentStorageApiFactory(_testRoot, dbPath);

        Guid dbStamp = Guid.NewGuid();
        User user;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            user = new User
            {
                Name = "Eval User",
                Email = "eval@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("eval123456"),
                Role = Roles.Admin,
                Status = "Active",
                SessionVersion = 1,
                SecurityStamp = dbStamp,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(StaffCookieEvents.SessionVersionClaim, "1"),
        };

        switch (stampCase)
        {
            case "missing":
                // No security_stamp claim added
                break;
            case "empty_string":
                claims.Add(new Claim(StaffCookieEvents.SecurityStampClaim, ""));
                break;
            case "guid_empty":
                claims.Add(new Claim(StaffCookieEvents.SecurityStampClaim, Guid.Empty.ToString()));
                break;
            case "malformed":
                claims.Add(new Claim(StaffCookieEvents.SecurityStampClaim, "not-a-valid-guid-xyz"));
                break;
            case "wrong_guid":
                claims.Add(new Claim(StaffCookieEvents.SecurityStampClaim, Guid.NewGuid().ToString()));
                break;
            case "matching_guid":
                claims.Add(new Claim(StaffCookieEvents.SecurityStampClaim, dbStamp.ToString("D")));
                break;
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        }, scheme);

        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
        var cookieValue = options.TicketDataFormat.Protect(ticket);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", $"prophetops={cookieValue}");

        var resp = await client.GetAsync("/api/auth/me");
        Assert.Equal(expectedStatus, resp.StatusCode);
    }

    [Fact]
    public async Task Account_mutations_rotate_security_stamp_and_wrap_session_version_safely_on_max_int()
    {
        var dbPath = Path.Combine(_testRoot, "max_int_test.db");
        using var factory = new PersistentStorageApiFactory(_testRoot, dbPath);

        Guid initialStamp = Guid.NewGuid();
        User adminUser;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Users.Add(new User
            {
                Name = "Owner",
                Email = "owner@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
                Role = Roles.OwnerManagement,
                Status = "Active",
                SessionVersion = 1,
                SecurityStamp = Guid.NewGuid(),
            });
            adminUser = new User
            {
                Name = "Admin",
                Email = "admin@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = Roles.Admin,
                Status = "Active",
                SessionVersion = int.MaxValue, // Test max-int edge
                SecurityStamp = initialStamp,
            };
            db.Users.Add(adminUser);
            await db.SaveChangesAsync();
        }

        using var ownerClient = await AuthenticatedClient.Login(factory, "owner@prophetops.local", "owner123");

        // Mutate admin's password: wraps SessionVersion safely to 1, rotates stamp
        var update = await ownerClient.PutAsJsonAsync("/api/users/admin@prophetops.local", new
        {
            name = "Admin",
            email = "admin@prophetops.local",
            role = Roles.Admin,
            status = "Active",
            password = "newadminpassword123",
        });
        update.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refreshed = await db.Users.SingleAsync(u => u.Email == "admin@prophetops.local");
            Assert.Equal(1, refreshed.SessionVersion); // Safe wrap without overflow
            Assert.NotEqual(initialStamp, refreshed.SecurityStamp);
            Assert.NotEqual(Guid.Empty, refreshed.SecurityStamp);
        }
    }

    [Fact]
    public async Task Logout_rotates_security_stamp_and_rejects_subsequent_requests()
    {
        var dbPath = Path.Combine(_testRoot, "logout_rotation_test.db");
        using var factory = new PersistentStorageApiFactory(_testRoot, dbPath);

        Guid initialStamp = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Users.Add(new User
            {
                Name = "Owner",
                Email = "owner@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
                Role = Roles.OwnerManagement,
                Status = "Active",
                SessionVersion = 1,
                SecurityStamp = initialStamp,
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "owner@prophetops.local", password = "owner123" });
        login.EnsureSuccessStatusCode();

        var authCookies = login.Headers.GetValues("Set-Cookie").ToList();
        var cookieHeader = authCookies.First(c => c.StartsWith("prophetops=")).Split(';')[0];

        // Valid before logout
        var meBefore = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);

        // Perform logout
        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Verify stamp in DB was rotated
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "owner@prophetops.local");
            Assert.NotEqual(initialStamp, user.SecurityStamp);
            Assert.NotEqual(Guid.Empty, user.SecurityStamp);
        }

        // Even if attacker captured the cookie before logout, it is rejected against the rotated stamp in DB
        var attackerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        attackerClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        var meAfter = await attackerClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);
    }

    [Fact]
    public async Task Logout_when_maintenance_gate_denied_clears_local_cookie_returns_503_and_leaves_session_valid_until_retry()
    {
        var dbPath = Path.Combine(_testRoot, "logout_maint_denied.db");
        using var factory = new PersistentStorageApiFactory(_testRoot, dbPath);

        Guid initialStamp = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Users.Add(new User
            {
                Name = "Maintenance User",
                Email = "maint@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("maintPass123!"),
                Role = Roles.Admin,
                Status = "Active",
                SessionVersion = 1,
                SecurityStamp = initialStamp,
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "maint@prophetops.local", password = "maintPass123!" });
        login.EnsureSuccessStatusCode();

        var authCookies = login.Headers.GetValues("Set-Cookie").ToList();
        var cookieHeader = authCookies.First(c => c.StartsWith("prophetops=")).Split(';')[0];

        // Valid before logout
        var meBefore = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);

        // Put maintenance gate into maintenance mode
        var gate = factory.Services.GetRequiredService<MaintenanceGate>();
        var lease = await gate.TryBeginBackupCaptureAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.NotNull(lease);

        try
        {
            // Call logout while maintenance is closed
            var logout = await client.PostAsync("/api/auth/logout", null);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, logout.StatusCode);
            Assert.True(logout.Headers.Contains("Retry-After"));

            // Local cookie was cleared by SignOutAsync even on 503
            var logoutCookies = logout.Headers.GetValues("Set-Cookie").ToList();
            Assert.Contains(logoutCookies, c => c.StartsWith("prophetops="));

            // Copied ticket remains VALID because server revocation was denied and stamp was NOT rotated
            var copiedTicketClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
            copiedTicketClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
            var meWithCopied = await copiedTicketClient.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, meWithCopied.StatusCode);

            // Verify stamp in DB was NOT rotated
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await db.Users.SingleAsync(u => u.Email == "maint@prophetops.local");
                Assert.Equal(initialStamp, user.SecurityStamp);
            }
        }
        finally
        {
            lease?.Dispose();
        }

        // Retry logout now that maintenance is open
        var retryLogoutClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        retryLogoutClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        var retryLogout = await retryLogoutClient.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, retryLogout.StatusCode);

        // After successful retry, the copied ticket is now REVOKED
        var meAfterRetry = await retryLogoutClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterRetry.StatusCode);
    }

    [Fact]
    public async Task Logout_when_db_commit_fails_clears_local_cookie_returns_503_and_leaves_session_valid_until_retry()
    {
        var dbPath = Path.Combine(_testRoot, "logout_db_failure.db");
        var interceptor = new TestCommitFailureInterceptor();
        using var factory = new PersistentStorageApiFactory(_testRoot, dbPath, services =>
        {
            services.AddSingleton(interceptor);
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseSqlite($"Data Source={dbPath}");
                options.AddInterceptors(sp.GetRequiredService<TestCommitFailureInterceptor>());
            });
        });

        Guid initialStamp = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Users.Add(new User
            {
                Name = "Fail User",
                Email = "fail@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("failPass123!"),
                Role = Roles.Admin,
                Status = "Active",
                SessionVersion = 1,
                SecurityStamp = initialStamp,
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "fail@prophetops.local", password = "failPass123!" });
        login.EnsureSuccessStatusCode();

        var authCookies = login.Headers.GetValues("Set-Cookie").ToList();
        var cookieHeader = authCookies.First(c => c.StartsWith("prophetops=")).Split(';')[0];

        // Valid before logout
        var meBefore = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);

        // Inject commit failure
        interceptor.FailOnSave = true;

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, logout.StatusCode);
        Assert.True(logout.Headers.Contains("Retry-After"));

        // Verify structured generic error without raw exceptions or secrets
        var errorBody = await logout.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("revocation_failed", errorBody?["code"]);
        Assert.False(string.IsNullOrWhiteSpace(errorBody?["message"]));
        Assert.DoesNotContain("Injected DB failure", errorBody?["message"] ?? "");

        // Local cookie was cleared by SignOutAsync
        var logoutCookies = logout.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(logoutCookies, c => c.StartsWith("prophetops="));

        // Copied ticket remains VALID because server revocation failed
        var copiedTicketClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        copiedTicketClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        var meWithCopied = await copiedTicketClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meWithCopied.StatusCode);

        // Stamp in DB remains untouched
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "fail@prophetops.local");
            Assert.Equal(initialStamp, user.SecurityStamp);
        }

        // Restore normal DB operations and retry
        interceptor.FailOnSave = false;

        var retryLogoutClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        retryLogoutClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        var retryLogout = await retryLogoutClient.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, retryLogout.StatusCode);

        // After successful retry, ticket is revoked
        var meAfterRetry = await retryLogoutClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterRetry.StatusCode);
    }

    [Fact]
    public async Task Logout_with_stale_actor_inside_lock_does_not_rotate_newer_sessions()
    {
        var dbPath = Path.Combine(_testRoot, "logout_stale_actor.db");
        using var factory = new PersistentStorageApiFactory(_testRoot, dbPath);

        Guid initialStamp = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Users.Add(new User
            {
                Name = "Actor User",
                Email = "actor@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("actorPass123!"),
                Role = Roles.Admin,
                Status = "Active",
                SessionVersion = 1,
                SecurityStamp = initialStamp,
            });
            await db.SaveChangesAsync();
        }

        // Device A logs in -> gets Cookie A
        var clientA = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var loginA = await clientA.PostAsJsonAsync("/api/auth/login", new { email = "actor@prophetops.local", password = "actorPass123!" });
        loginA.EnsureSuccessStatusCode();
        var cookieHeaderA = loginA.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("prophetops=")).Split(';')[0];

        // Simulate password change / previous revocation that rotates stamp to S2 and advances version to 2
        Guid newerStamp = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "actor@prophetops.local");
            user.SecurityStamp = newerStamp;
            user.SessionVersion = 2;
            await db.SaveChangesAsync();
        }

        // Device B logs in with new credentials -> gets Cookie B
        var clientB = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var loginB = await clientB.PostAsJsonAsync("/api/auth/login", new { email = "actor@prophetops.local", password = "actorPass123!" });
        loginB.EnsureSuccessStatusCode();
        var cookieHeaderB = loginB.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("prophetops=")).Split(';')[0];

        // Verify Device B is active
        var testB = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        testB.DefaultRequestHeaders.Add("Cookie", cookieHeaderB);
        var meB = await testB.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meB.StatusCode);

        // Device A (stale actor) calls logout with Cookie A
        var staleLogoutClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        staleLogoutClient.DefaultRequestHeaders.Add("Cookie", cookieHeaderA);
        var staleLogout = await staleLogoutClient.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, staleLogout.StatusCode);

        // Device B's newer session MUST NOT be rotated or invalidated!
        var meBAfterStaleLogout = await testB.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meBAfterStaleLogout.StatusCode);

        // SecurityStamp in DB remains Device B's stamp
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "actor@prophetops.local");
            Assert.Equal(newerStamp, user.SecurityStamp);
            Assert.Equal(2, user.SessionVersion);
        }
    }

    [Fact]
    public async Task Logout_success_revokes_all_issued_tickets_for_that_user()
    {
        var dbPath = Path.Combine(_testRoot, "logout_revokes_all.db");
        using var factory = new PersistentStorageApiFactory(_testRoot, dbPath);

        Guid initialStamp = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Users.Add(new User
            {
                Name = "Multi Ticket User",
                Email = "multi@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("multiPass123!"),
                Role = Roles.Staff,
                Status = "Active",
                SessionVersion = 1,
                SecurityStamp = initialStamp,
            });
            await db.SaveChangesAsync();
        }

        // Login on Device 1
        var client1 = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login1 = await client1.PostAsJsonAsync("/api/auth/login", new { email = "multi@prophetops.local", password = "multiPass123!" });
        login1.EnsureSuccessStatusCode();
        var cookieHeader1 = login1.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("prophetops=")).Split(';')[0];

        // Login on Device 2
        var client2 = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login2 = await client2.PostAsJsonAsync("/api/auth/login", new { email = "multi@prophetops.local", password = "multiPass123!" });
        login2.EnsureSuccessStatusCode();
        var cookieHeader2 = login2.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("prophetops=")).Split(';')[0];

        // Both tickets are initially valid
        var testClient1 = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        testClient1.DefaultRequestHeaders.Add("Cookie", cookieHeader1);
        var me1 = await testClient1.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me1.StatusCode);

        var testClient2 = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        testClient2.DefaultRequestHeaders.Add("Cookie", cookieHeader2);
        var me2 = await testClient2.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me2.StatusCode);

        // Device 1 performs logout
        var logout = await client1.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // BOTH Device 1 and Device 2 tickets are now REVOKED
        var me1After = await testClient1.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me1After.StatusCode);

        var me2After = await testClient2.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me2After.StatusCode);
    }

    [Fact]
    public void Populated_sqlite_migration_upgrade_backfills_distinct_valid_guids()
    {
        var dbPath = Path.Combine(_testRoot, "sqlite_upgrade_test.db");

        // 1. Create SQLite DB and migrate up to 20261002050000_AddDataProtectionKeys
        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;

        using (var db = new AppDbContext(options))
        {
            db.GetService<IMigrator>().Migrate("20261002050000_AddDataProtectionKeys");

            // Insert 5 user rows without SecurityStamp
            for (var i = 1; i <= 5; i++)
            {
                var name = $"User {i}";
                var email = $"user{i}@test.local";
                var hash = $"hash{i}";
                var role = Roles.Staff;
                db.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO \"Users\" (\"Id\", \"Name\", \"Email\", \"PasswordHash\", \"Role\", \"Status\", \"SessionVersion\") VALUES ({i}, {name}, {email}, {hash}, {role}, 'Active', 1);");
            }
        }

        // 2. Apply full migration to latest (which includes AddUserSecurityStamp)
        using (var db = new AppDbContext(options))
        {
            db.Database.Migrate();

            // 3. Assertions
            var users = db.Users.OrderBy(u => u.Id).ToList();
            Assert.Equal(5, users.Count);

            var stamps = users.Select(u => u.SecurityStamp).ToList();

            // None are Guid.Empty
            Assert.All(stamps, s => Assert.NotEqual(Guid.Empty, s));

            // All are distinct
            Assert.Equal(5, stamps.Distinct().Count());

            // 4. Verify round-trip formatting in SQLite: 36 lowercase hyphenated characters
            using var conn = new SqliteConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SecurityStamp FROM Users;";
            using var reader = cmd.ExecuteReader();
            var guidPattern = new Regex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");

            var rawCount = 0;
            while (reader.Read())
            {
                rawCount++;
                var rawStamp = reader.GetString(0);
                Assert.True(guidPattern.IsMatch(rawStamp), $"SecurityStamp '{rawStamp}' does not match standard GUID format.");
                Assert.True(Guid.TryParse(rawStamp, out var parsed) && parsed != Guid.Empty);
            }
            Assert.Equal(5, rawCount);
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch { }
    }

    private sealed class TestCommitFailureInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public bool FailOnSave { get; set; }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (FailOnSave)
            {
                throw new DbUpdateException("Injected DB failure for testing");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class PersistentStorageApiFactory(
        string storageRoot,
        string dbPath,
        Action<IServiceCollection>? configureCustomServices = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = "false",
                    ["Storage:Root"] = storageRoot,
                    ["Business:TimeZone"] = "Asia/Manila",
                }));

            builder.ConfigureServices(services =>
            {
                var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions)).ToList();
                foreach (var d in toRemove) services.Remove(d);

                if (configureCustomServices != null)
                {
                    configureCustomServices(services);
                }
                else
                {
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseSqlite($"Data Source={dbPath}"));
                }
            });
        }
    }
}
