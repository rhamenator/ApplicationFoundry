using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ApplicationFoundry.Components;
using ApplicationFoundry.Components.Account;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features;
using ApplicationFoundry.Features.Documents;
using ApplicationFoundry.Features.Fit;
using ApplicationFoundry.Features.Jobs;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Diagnostics;
using System.Security.Claims;

var desktopMode = args.Any(arg => string.Equals(arg, "--desktop", StringComparison.OrdinalIgnoreCase));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args.Where(arg => !string.Equals(arg, "--desktop", StringComparison.OrdinalIgnoreCase)).ToArray(),
    // Packaged binaries may be launched from any working directory.
    ContentRootPath = AppContext.BaseDirectory
});
if (desktopMode)
{
    // A random loopback port avoids conflicts with other local web applications.
    builder.WebHost.UseUrls("http://127.0.0.1:0");
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var dataDirectory = builder.Configuration["ApplicationFoundry:DataDirectory"];
    if (string.IsNullOrWhiteSpace(dataDirectory))
    {
        dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ApplicationFoundry");
    }

    Directory.CreateDirectory(dataDirectory);
    connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(dataDirectory, "app.db"),
        Cache = SqliteCacheMode.Shared
    }.ToString();
}
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddSingleton<IFitScorer, OnnxFitScorer>();
builder.Services.AddCareerFeatures();
builder.Services.AddSingleton<IClientDocumentExport, BrowserDocumentExport>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Installed applications must be usable on first launch, without the EF CLI.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!desktopMode) app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();
app.MapHealthChecks("/health");

app.MapGet("/documents/{id:int}/{format}", async (
    int id,
    string format,
    ClaimsPrincipal principal,
    IDbContextFactory<ApplicationDbContext> factory,
    ICareerDocumentService documents) =>
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    await using var db = await factory.CreateDbContextAsync();
    var document = await db.DocumentVersions
        .Join(db.JobOpportunities,
            document => document.JobOpportunityId,
            job => job.Id,
            (document, job) => new { Document = document, job.UserId })
        .SingleOrDefaultAsync(item => item.Document.Id == id && item.UserId == userId);
    if (document is null) return Results.NotFound();
    if (document.Document.ApprovedAt is null)
    {
        return Results.Problem("Approve this version before export.", statusCode: StatusCodes.Status409Conflict);
    }
    var safeName = $"application-{document.Document.Id}";
    return format.ToLowerInvariant() switch
    {
        "docx" => Results.File(documents.ToDocx(document.Document.Content),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{safeName}.docx"),
        "pdf" => Results.File(documents.ToPdf(document.Document.Content), "application/pdf", $"{safeName}.pdf"),
        _ => Results.NotFound()
    };
}).RequireAuthorization();

if (desktopMode)
{
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()?
        .Addresses.SingleOrDefault(value => value.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase));
    if (address is null) throw new InvalidOperationException("The desktop server did not bind to a loopback address.");

    try
    {
        Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
    }
    catch (Exception exception)
    {
        app.Logger.LogWarning(exception, "Open {Address} in a browser to use Application Foundry.", address);
    }

    await app.WaitForShutdownAsync();
}
else
{
    await app.RunAsync();
}

public partial class Program;
