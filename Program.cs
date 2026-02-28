using LedgerFlow.Background;
using LedgerFlow.Components;
using LedgerFlow.Data;
using LedgerFlow.Models;
using LedgerFlow.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LedgerFlow.Services.Extraction;
using LedgerFlow.Services.Validation;


var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Identity (email + password)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Options
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

// Background processing
builder.Services.AddSingleton<IInvoiceProcessingQueue, InvoiceProcessingQueue>();
builder.Services.AddHostedService<InvoiceProcessingWorker>();

// MVC / API
builder.Services.AddControllers();

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

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();