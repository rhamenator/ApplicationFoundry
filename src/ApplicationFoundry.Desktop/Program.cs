using System.Security.Claims;
using ApplicationFoundry.Components.Pages;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Documents;
using ApplicationFoundry.Features.Fit;
using ApplicationFoundry.Features.Jobs;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ApplicationFoundry.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            Run();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.ToString(), "Application Foundry startup error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void Run()
    {
        ApplicationConfiguration.Initialize();

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ApplicationFoundry");
        Directory.CreateDirectory(dataDirectory);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "app.db"),
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, DesktopAuthenticationStateProvider>();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        }).AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<IFitScorer, ManagedFitScorer>();
        services.AddHttpClient<IJobDiscoveryService, RemoteJobDiscoveryService>();
        services.AddHttpClient<GreenhouseJobDiscoveryService>();
        services.AddSingleton<ICareerDocumentService, CareerDocumentService>();
        services.AddSingleton<ICandidateDocumentImportService, CandidateDocumentImportService>();
        var desktopExport = new DesktopDocumentExport();
        services.AddSingleton<IClientDocumentExport>(desktopExport);

        using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();

        Application.Run(new DesktopWindow(provider, desktopExport));
    }
}

internal sealed class DesktopWindow : Form
{
    public DesktopWindow(IServiceProvider services, DesktopDocumentExport desktopExport)
    {
        desktopExport.Owner = this;
        Text = "Application Foundry";
        Width = 1280;
        Height = 850;
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var view = new BlazorWebView
        {
            Dock = DockStyle.Fill,
            HostPage = "wwwroot/index.html",
            Services = services
        };
        view.RootComponents.Add<Studio>("#app");
        Controls.Add(view);
    }
}

internal sealed class DesktopDocumentExport : IClientDocumentExport
{
    public Form? Owner { get; set; }
    public bool IsAvailable => true;

    public Task<bool> SaveAsync(byte[] content, string fileName, string mediaType)
    {
        var owner = Owner ?? throw new InvalidOperationException("The desktop window is unavailable.");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void SaveOnUiThread()
        {
            try
            {
                using var dialog = new SaveFileDialog
                {
                    FileName = fileName,
                    Filter = mediaType == "application/pdf" ? "PDF document (*.pdf)|*.pdf" : "Word document (*.docx)|*.docx",
                    AddExtension = true,
                    OverwritePrompt = true
                };
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                {
                    completion.SetResult(false);
                    return;
                }
                File.WriteAllBytes(dialog.FileName, content);
                completion.SetResult(true);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }
        if (owner.InvokeRequired) owner.BeginInvoke(SaveOnUiThread);
        else SaveOnUiThread();
        return completion.Task;
    }
}

internal sealed class DesktopAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState State = new(new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "desktop-local")], "Local desktop")));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(State);
}
