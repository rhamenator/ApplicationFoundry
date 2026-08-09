using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ApplicationFoundry.Components;
using ApplicationFoundry.Components.Account;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Documents;
using ApplicationFoundry.Features.Fit;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
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
builder.Services.AddSingleton<ICareerDocumentService, CareerDocumentService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

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
app.UseHttpsRedirection();

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

app.Run();

public partial class Program;
