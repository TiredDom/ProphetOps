using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;
using ProphetOps.Domain;

var standalone = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "")
    .Equals("ProphetOps.Api", StringComparison.OrdinalIgnoreCase);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = standalone ? AppContext.BaseDirectory : null,
});

builder.Host.UseWindowsService();

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var dbConnection = builder.Configuration.GetConnectionString("Default")
    ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "prophetops.db")}";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(dbConnection));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "prophetops";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

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

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("spa");
app.UseAuthentication();
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
            Secure = context.Request.IsHttps,
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

app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }
