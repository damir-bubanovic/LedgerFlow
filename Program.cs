using System.IO;
using System.Text.Json;
using LedgerFlow.Background;
using LedgerFlow.Components;
using LedgerFlow.Data;
using LedgerFlow.Models;
using LedgerFlow.Options;
using LedgerFlow.Services.Extraction;
using LedgerFlow.Services.Validation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Security settings from configuration
var requireConfirmedAccount = builder.Configuration.GetValue<bool>("Security:RequireConfirmedAccount");
var cookieExpireHours = builder.Configuration.GetValue<int>("Security:CookieExpireHours", 8);
var lockoutMinutes = builder.Configuration.GetValue<int>("Security:LockoutMinutes", 10);
var maxFailedAccessAttempts = builder.Configuration.GetValue<int>("Security:MaxFailedAccessAttempts", 5);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Use DbContextFactory for Blazor components (avoids long-lived DbContext issues)
// IMPORTANT: make DbContextFactory scoped to avoid singleton->scoped dependency issues (EF tooling)
builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options => options.UseNpgsql(connectionString),
    ServiceLifetime.Scoped);

// Data Protection key persistence
var keysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(keysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("LedgerFlow");

// Forwarded headers support for reverse proxies
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Identity (email + password)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = requireConfirmedAccount;

    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutMinutes);
    options.Lockout.MaxFailedAccessAttempts = maxFailedAccessAttempts;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// Secure authentication cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "LedgerFlow.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(cookieExpireHours);
});

// Authorization for Blazor components
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Options
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

// Background processing
builder.Services.AddSingleton<IInvoiceProcessingQueue, InvoiceProcessingQueue>();
builder.Services.AddHostedService<InvoiceProcessingWorker>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddDbContextCheck<ApplicationDbContext>("database", tags: new[] { "ready" });

// MVC / API
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();

// Razor / Blazor
builder.Services.AddRazorPages();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<IInvoiceExtractor, StubInvoiceExtractor>();
builder.Services.AddSingleton<IInvoiceValidator, BasicInvoiceValidator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = app.Environment.IsDevelopment()
        ? WriteDetailedHealthResponse
        : WriteMinimalHealthResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = app.Environment.IsDevelopment()
        ? WriteDetailedHealthResponse
        : WriteMinimalHealthResponse
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = app.Environment.IsDevelopment()
        ? WriteDetailedHealthResponse
        : WriteMinimalHealthResponse
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static Task WriteMinimalHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var payload = new
    {
        status = report.Status.ToString()
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

static Task WriteDetailedHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var payload = new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            duration = entry.Value.Duration.TotalMilliseconds,
            description = entry.Value.Description,
            error = entry.Value.Exception?.Message
        })
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
        WriteIndented = true
    }));
}