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

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args.Where(arg => arg != "--bootstrap-owner").ToArray(),
    ContentRootPath = standalone ? baseDir : null,
});

builder.Host.UseWindowsService();

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
ContainerPortBinding.Configure(builder, args);

var containerHosted = HostedRuntime.IsEnabled(builder.Configuration);
builder.Services.AddSingleton(sp => StoragePaths.FromConfiguration(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<IHostEnvironment>()));
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
    var dbConnection = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
        ?? sp.GetRequiredService<StoragePaths>().DatabaseConnectionString;
    options.UseSqlite(dbConnection);
});
builder.Services.AddScoped<StaffCookieEvents>();
builder.Services.AddScoped<MutationTransaction>();
builder.Services.AddScoped<BookingMutationService>();
builder.Services.AddScoped<BackupPackageWriter>();
builder.Services.AddScoped<IBackupStorage>(BackupStorageFactory.Create);
builder.Services.AddScoped<IBackupPackageFileOperations, BackupPackageFileOperations>();
builder.Services.AddDataProtection()
    .SetApplicationName("ProphetOps");
builder.Services.AddOptions<KeyManagementOptions>().Configure<StoragePaths>((options, paths) =>
    options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(paths.KeysPath), NullLoggerFactory.Instance));

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
var publicTransport = app.Services.GetRequiredService<PublicTransportOptions>();
BackupStorageFactory.ValidateHostedSchedule(app.Configuration);

var demoEnabled = app.Configuration.GetValue<bool>("Demo:Enabled");
if (demoEnabled && app.Environment.IsProduction())
    throw new InvalidOperationException("Production cannot run with demonstration data enabled.");

static void CreateBootstrapOwner(AppDbContext db)
{
    ProductionBootstrap.CreateOwner(db,
        Environment.GetEnvironmentVariable("Bootstrap__OwnerName"),
        Environment.GetEnvironmentVariable("Bootstrap__OwnerEmail"),
        Environment.GetEnvironmentVariable("Bootstrap__OwnerPassword"));
}

if (bootstrapOwner)
{
    try
    {
        if (demoEnabled) throw new InvalidOperationException("Owner setup cannot run in demonstration mode.");
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        CreateBootstrapOwner(db);
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    if (app.Configuration.GetValue("Bootstrap:Owner:Enabled", false))
    {
        if (demoEnabled) throw new InvalidOperationException("Owner setup cannot run in demonstration mode.");
        var result = ProductionBootstrap.CreateOwnerIfEmpty(db,
            Environment.GetEnvironmentVariable("Bootstrap__OwnerName"),
            Environment.GetEnvironmentVariable("Bootstrap__OwnerEmail"),
            Environment.GetEnvironmentVariable("Bootstrap__OwnerPassword"));
        if (result.Created)
            app.Logger.LogInformation("Owner setup completed. For durable environments, remove Bootstrap:Owner:Enabled and the setup credentials after first start.");
        else
            app.Logger.LogInformation("Owner setup skipped because an account already exists. Existing account credentials were left unchanged.");
    }
    else if (demoEnabled) DbSeeder.Seed(db);
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
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }
