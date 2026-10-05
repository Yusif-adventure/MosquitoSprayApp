using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SmartMosquitoControl.Data;
using SmartMosquitoControl.Middleware;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;
using AuthenticationService = SmartMosquitoControl.Services.AuthenticationService;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var isDevelopment = builder.Environment.IsDevelopment();

// Structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ── Storage ───────────────────────────────────────────────────────────────────
// One database provider (SQLite) for every environment so the migrations always match what runs.
// Everything mutable (database + data-protection keys) lives under one directory that can be a Docker volume.
var dataDirectory = Path.GetFullPath(config["Data:Directory"] ?? "data", builder.Environment.ContentRootPath);
Directory.CreateDirectory(dataDirectory);

var connectionString = config.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.Combine(dataDirectory, "mosquitocontrol.db")}";

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

// Without persisted keys, every container restart would invalidate all sign-in cookies and antiforgery tokens.
builder.Services.AddDataProtection()
    .SetApplicationName("SmartMosquitoControl")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys")));

// ── Identity & authentication ─────────────────────────────────────────────────
// Email confirmation is required outside Development unless explicitly turned off.
var requireConfirmedEmail = config.GetValue("Auth:RequireConfirmedEmail", !isDevelopment);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = requireConfirmedEmail;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// Google sign-in is only registered when real credentials are configured
// (User Secrets / environment variables: Authentication:Google:ClientId and :ClientSecret).
var googleClientId = config["Authentication:Google:ClientId"];
var googleClientSecret = config["Authentication:Google:ClientSecret"];
var googleEnabled = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);

if (googleEnabled)
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId!;
            options.ClientSecret = googleClientSecret!;

            // Needed to refuse to trust unverified email addresses (see AccountController.ExternalLoginCallback).
            options.ClaimActions.MapJsonKey("email_verified", "email_verified");

            // Force the Google account selection screen every time
            options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
            {
                OnRedirectToAuthorizationEndpoint = context =>
                {
                    context.Response.Redirect(context.RedirectUri + "&prompt=select_account");
                    return Task.CompletedTask;
                }
            };
        });
}
builder.Services.AddSingleton(new ExternalAuthInfo(googleEnabled));

// ── Abuse protection ──────────────────────────────────────────────────────────
// Partitioned by client IP. Behind a reverse proxy, set ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
// so this sees the real client address rather than the proxy's.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string Client(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        Client(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    options.AddPolicy("device", context => RateLimitPartition.GetFixedWindowLimiter(
        Client(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// ── Application services ──────────────────────────────────────────────────────
// With no physical sprayer (Development by default) the server completes spray commands itself.
builder.Services.Configure<DeviceOptions>(options =>
    options.SimulateHardware = config.GetValue("Device:SimulateHardware", isDevelopment));

builder.Services.Configure<EmailOptions>(config.GetSection("Email"));
var emailOptions = config.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
if (emailOptions.IsConfigured)
{
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, LoggingEmailService>();
}

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DeviceCommandService>();
builder.Services.AddScoped<MosquitoDataService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<ScheduleProcessor>();

if (config.GetValue("Scheduler:Enabled", true))
{
    builder.Services.AddHostedService<ScheduleRunnerService>();
}

var app = builder.Build();

// Apply pending migrations and create the database if needed
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();

    // Write-ahead logging lets the scheduler and web requests use the database concurrently.
    dbContext.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}

if (requireConfirmedEmail && !emailOptions.IsConfigured)
{
    app.Logger.LogWarning(
        "Email confirmation is required but no SMTP server is configured (Email:Host / Email:From). " +
        "Confirmation links will only appear in the log, so new users cannot sign in until you configure email " +
        "or set Auth:RequireConfirmedEmail=false.");
}

// Unhandled exceptions: pages get the /Home/Error page with a real HTTP 500; /api gets JSON.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseGlobalExceptionHandling();

// Containers usually sit behind a TLS-terminating proxy and only speak HTTP, so redirecting there
// would just log a warning (or loop). Override with Https:Redirect.
var runningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
if (config.GetValue("Https:Redirect", !runningInContainer))
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
