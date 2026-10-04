using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProphetOps.Api;
using ProphetOps.Data;
using ProphetOps.Domain;

var baseDir = AppContext.BaseDirectory;
var standalone = Directory.Exists(Path.Combine(baseDir, "wwwroot"));
var bootstrapOwner = args.Contains("--bootstrap-owner", StringComparer.Ordinal);
var migrateDatabase = args.Contains("--migrate-database", StringComparer.Ordinal);
var restorePrivateObjects = args.Contains("--restore-private-objects", StringComparer.Ordinal)
    || args.Contains("--recover-private-objects", StringComparer.Ordinal);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args.Where(arg => arg != "--bootstrap-owner" && arg != "--migrate-database" && arg != "--restore-private-objects" && arg != "--recover-private-objects").ToArray(),
    ContentRootPath = standalone ? baseDir : null,
});

builder.Host.UseWindowsService();

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
ContainerPortBinding.Configure(builder, args);

var containerHosted = HostedRuntime.IsEnabled(builder.Configuration);
builder.Services.AddSingleton(sp => StoragePaths.FromConfiguration(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<IHostEnvironment>()));
builder.Services.AddSingleton(sp =>
{
    var storage = sp.GetRequiredService<StoragePaths>();
    return DatabaseRuntimeOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>(), storage.DatabaseConnectionString);
});
builder.Services.AddSingleton(sp => CloudflareAccessOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(sp => PublicTransportOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBusinessClock, BusinessClock>();
builder.Services.AddSingleton<MaintenanceGate>();
builder.Services.AddHttpClient<ICloudflareAccessJwksTransport, HttpCloudflareAccessJwksTransport>(client =>
    client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<ICloudflareAccessKeyStore, CloudflareAccessJwksCache>();

if (builder.Environment.IsProduction() && containerHosted)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
}

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var database = sp.GetRequiredService<DatabaseRuntimeOptions>();
    DatabaseConfiguration.Configure(options, database.Provider, database.ConnectionString, database.MigrationsAssembly);
});
builder.Services.AddScoped<StaffCookieEvents>();
builder.Services.AddScoped<MutationTransaction>();
builder.Services.AddScoped<BookingMutationService>();
builder.Services.AddSingleton<IObjectStorage>(ObjectStorageFactory.Create);
builder.Services.AddSingleton<IObjectRetentionPolicy, ConservativeObjectRetentionPolicy>();
builder.Services.AddScoped<ObjectCleanupService>();
builder.Services.AddSingleton<IPostgresProcessRunner, PostgresProcessRunner>();
builder.Services.AddScoped<SqliteBackupCapture>();
builder.Services.AddScoped<PostgresBackupCapture>();
builder.Services.AddScoped<IDatabaseBackupCapture>(sp =>
{
    var database = sp.GetRequiredService<DatabaseRuntimeOptions>();
    return database.Provider == DatabaseProviderKind.Postgres
        ? sp.GetRequiredService<PostgresBackupCapture>()
        : sp.GetRequiredService<SqliteBackupCapture>();
});
builder.Services.AddScoped<BackupPackageWriter>();
builder.Services.AddScoped<IBackupStorage>(BackupStorageFactory.Create);
builder.Services.AddScoped<IBackupPackageFileOperations, BackupPackageFileOperations>();
var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("ProphetOps");
builder.Services.AddSingleton<PostgresXmlRepository>();
var configuredProvider = DataProtectionConfiguration.DetermineProvider(builder.Configuration);
DataProtectionConfiguration.Configure(
    dpBuilder,
    builder.Services,
    builder.Configuration,
    configuredProvider,
    containerHosted,
    isOfflineCommand: bootstrapOwner || migrateDatabase || restorePrivateObjects);
builder.Services.AddOptions<KeyManagementOptions>().Configure<DatabaseRuntimeOptions, StoragePaths, IServiceProvider>((options, database, paths, sp) =>
{
    if (database.Provider == DatabaseProviderKind.Postgres)
    {
        options.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>();
    }
    else
    {
        options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(paths.KeysPath), NullLoggerFactory.Instance);
    }
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "prophetops";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(StaffCookieEvents);
    });
builder.Services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    if (PublicTransportOptions.FromConfiguration(builder.Configuration).RequireSecureCookies)
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization(options =>
{
    foreach (var label in new[] { "Dashboard", "Bookings", "Package Catalog", "Expenses", "Analytics", "Forecast", "Reports", "Users" })
    {
        var permission = label;
        options.AddPolicy(permission, policy => policy.RequireAssertion(context =>
            Roles.CanAccess(context.User.FindFirst(ClaimTypes.Role)?.Value, permission)));
    }
});

builder.Services.AddCors(options =>
    options.AddPolicy("spa", policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddControllers();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");

builder.Services.AddSingleton(new SignInThrottle());
builder.Services.AddHostedService<BackupService>();

await using var app = builder.Build();
var hosted = HostedRuntime.IsEnabled(app.Configuration);
_ = app.Services.GetRequiredService<StoragePaths>();
_ = app.Services.GetRequiredService<IBusinessClock>();
_ = app.Services.GetRequiredService<CloudflareAccessOptions>();
_ = app.Services.GetService<HostedDataProtectionCertificate>();
var publicTransport = app.Services.GetRequiredService<PublicTransportOptions>();

var demoEnabled = app.Configuration.GetValue<bool>("Demo:Enabled");
if (demoEnabled && app.Environment.IsProduction())
    throw new InvalidOperationException("Production cannot run with demonstration data enabled.");

static AppDbContext CreateCommandDb(IServiceProvider services, bool useMaintenanceConnection, out DatabaseRuntimeOptions database)
{
    var storage = services.GetRequiredService<StoragePaths>();
    database = DatabaseRuntimeOptions.FromConfiguration(
        services.GetRequiredService<IConfiguration>(),
        storage.DatabaseConnectionString,
        useMaintenanceConnection);
    var options = new DbContextOptionsBuilder<AppDbContext>();
    DatabaseConfiguration.Configure(options, database.Provider, database.ConnectionString, database.MigrationsAssembly);
    return new AppDbContext(options.Options);
}

static Task CreateBootstrapOwner(AppDbContext db, DatabaseProviderKind provider, CancellationToken cancellationToken) =>
    ProductionBootstrap.CreateOwnerAsync(db, provider,
        Environment.GetEnvironmentVariable("Bootstrap__OwnerName"),
        Environment.GetEnvironmentVariable("Bootstrap__OwnerEmail"),
        Environment.GetEnvironmentVariable("Bootstrap__OwnerPassword"),
        cancellationToken);

if (bootstrapOwner)
{
    try
    {
        if (demoEnabled) throw new InvalidOperationException("Owner setup cannot run in demonstration mode.");
        await using var db = CreateCommandDb(app.Services, useMaintenanceConnection: true, out var database);
        if (database.Provider == DatabaseProviderKind.Sqlite)
            await db.Database.MigrateAsync();
        await CreateBootstrapOwner(db, database.Provider, CancellationToken.None);
        app.Logger.LogInformation("Owner setup completed. Remove the setup credentials before starting the application.");
    }
    catch (InvalidOperationException ex)
    {
        app.Logger.LogError("Owner setup failed: {Reason}", ex.Message);
        Console.Error.WriteLine("Owner setup failed: " + ex.Message);
        Environment.ExitCode = 1;
    }
    return;
}

if (migrateDatabase)
{
    try
    {
        if (demoEnabled) throw new InvalidOperationException("Database migration cannot run in demonstration mode.");
        await using var db = CreateCommandDb(app.Services, useMaintenanceConnection: true, out _);
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Database migration completed.");
    }
    catch (InvalidOperationException ex)
    {
        app.Logger.LogError("Database migration failed: {Reason}", ex.Message);
        Console.Error.WriteLine("Database migration failed: " + ex.Message);
        Environment.ExitCode = 1;
    }
    return;
}

if (restorePrivateObjects)
{
    try
    {
        if (demoEnabled) throw new InvalidOperationException("Private object recovery cannot run in demonstration mode.");
        await using var db = CreateCommandDb(app.Services, useMaintenanceConnection: false, out _);
        var storage = app.Services.GetRequiredService<StoragePaths>();
        var objectStorage = app.Services.GetRequiredService<IObjectStorage>();
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var log = loggerFactory.CreateLogger<PrivateObjectRecoveryRunner>();

        var recoveryOptions = PrivateObjectRecoveryOptions.FromArgs(args, app.Configuration);

        ISupabaseRecoveryObjectCreator? recoveryCreator = null;
        var recoveryProjectUrl = app.Configuration["ObjectStorage:Recovery:SupabaseProjectUrl"];
        var recoveryApiKey = Environment.GetEnvironmentVariable("ObjectStorage__Recovery__SupabaseApiKey");
        if (!string.IsNullOrWhiteSpace(recoveryProjectUrl) && !string.IsNullOrWhiteSpace(recoveryApiKey))
        {
            recoveryCreator = new SupabaseRecoveryObjectCreator(
                app.Configuration,
                recoveryApiKey,
                recoveryPrefix: recoveryOptions.RecoveryPrefix);
        }

        using (recoveryCreator as IDisposable)
        {
            var runner = new PrivateObjectRecoveryRunner(db, app.Configuration, storage, objectStorage, log, recoveryCreator);
            var result = await runner.RunAsync(recoveryOptions, CancellationToken.None);

            if (result.Success)
            {
                app.Logger.LogInformation("Private object recovery completed: {PackageId}, Destination: {Destination}, Objects verified: {Verified}/{Expected}",
                    result.PackageId, result.DestinationIdentity, result.VerifiedObjectCount, result.ExpectedObjectCount);
                Console.WriteLine($"Private object recovery completed successfully. Package: {result.PackageId}, Destination: {result.DestinationIdentity}, Verified objects: {result.VerifiedObjectCount}/{result.ExpectedObjectCount}.");
                Environment.ExitCode = 0;
            }
            else
            {
                app.Logger.LogError("Private object recovery failed: Category: {Category}, Reason: {Reason}", result.FailureCategory, result.Message);
                Console.Error.WriteLine($"Private object recovery failed: [{result.FailureCategory}] {result.Message}");
                Environment.ExitCode = 1;
            }
        }
    }
    catch (Exception ex)
    {
        var errorType = ex.GetType().Name;
        app.Logger.LogError("Private object recovery failed with unexpected exception: {ErrorType}", errorType);
        Console.Error.WriteLine($"Private object recovery failed: unexpected error of type {errorType}.");
        Environment.ExitCode = 1;
    }
    return;
}

BackupStorageFactory.ValidateHostedSchedule(app.Configuration);
ObjectStorageFactory.ValidateHostedConfiguration(app.Configuration);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var database = scope.ServiceProvider.GetRequiredService<DatabaseRuntimeOptions>();
    var startupPlan = DatabaseStartupPlan.ForWeb(database.Provider);
    if (startupPlan.Migrate)
        db.Database.Migrate();
    if (startupPlan.ValidateSchemaOnly)
        await DatabaseReadiness.EnsureReadyAsync(db, CancellationToken.None);
    if (database.Provider == DatabaseProviderKind.Postgres)
    {
        var keyRepo = scope.ServiceProvider.GetRequiredService<PostgresXmlRepository>();
        keyRepo.GetAllElements();
    }
    if (startupPlan.AllowAutomaticOwnerBootstrap && app.Configuration.GetValue("Bootstrap:Owner:Enabled", false))
    {
        if (demoEnabled) throw new InvalidOperationException("Owner setup cannot run in demonstration mode.");
        var result = await ProductionBootstrap.CreateOwnerIfEmptyAsync(db, database.Provider,
            Environment.GetEnvironmentVariable("Bootstrap__OwnerName"),
            Environment.GetEnvironmentVariable("Bootstrap__OwnerEmail"),
            Environment.GetEnvironmentVariable("Bootstrap__OwnerPassword"),
            CancellationToken.None);
        if (result.Created)
            app.Logger.LogInformation("Owner setup completed. For durable environments, remove Bootstrap:Owner:Enabled and the setup credentials after first start.");
        else
            app.Logger.LogInformation("Owner setup skipped because an account already exists. Existing account credentials were left unchanged.");
    }
    else if (startupPlan.AllowDemoSeed && demoEnabled) DbSeeder.Seed(db);
    else if (!startupPlan.AllowAutomaticOwnerBootstrap && app.Configuration.GetValue("Bootstrap:Owner:Enabled", false))
    {
        throw new InvalidOperationException("Hosted PostgreSQL startup does not bootstrap an owner automatically. Run --bootstrap-owner after schema migration instead.");
    }
}

if (ForwardedHeadersSetup.HasTrustedBoundary(app.Configuration))
    app.UseForwardedHeaders(ForwardedHeadersSetup.FromConfiguration(app.Configuration));

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; base-uri 'self'; frame-ancestors 'none'; object-src 'none'";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Cache-Control"] = "no-store";
    await next();
});

app.UseMiddleware<CloudflareAccessMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("spa");
app.UseCookiePolicy(new CookiePolicyOptions
{
    OnAppendCookie = context =>
    {
        if (publicTransport.RequireSecureCookies)
            context.CookieOptions.Secure = true;
    },
});
app.UseAuthentication();
app.UseMiddleware<CloudflareAccessSessionBindingMiddleware>();
app.UseAuthorization();

var antiforgery = app.Services.GetRequiredService<IAntiforgery>();

app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            Secure = publicTransport.RequireSecureCookies || context.Request.IsHttps,
        });
    }
    await next();
});

app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var safe = HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
    var path = context.Request.Path;
    if (!safe && path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/auth"))
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid or missing antiforgery token." });
            return;
        }
    }
    await next();
});

app.MapGet(CloudflareAccessOptions.HealthPath, () => Results.Json(new { status = "ok" }));
app.MapGet(CloudflareAccessOptions.ReadyPath, async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var readiness = await DatabaseReadiness.CheckAsync(db, cancellationToken);
    return readiness.Ready
        ? Results.Json(new { status = readiness.Status })
        : Results.Json(new { status = readiness.Status }, statusCode: StatusCodes.Status503ServiceUnavailable);
});
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }
