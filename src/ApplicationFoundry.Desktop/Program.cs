using System.Diagnostics;
using System.Security.Claims;
using ApplicationFoundry.Components.Pages;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features;
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
    private static void Main(string[] args)
    {
        var selfTest = args.Contains("--self-test", StringComparer.OrdinalIgnoreCase);
        var timing = !selfTest && args.Contains("--startup-timing", StringComparer.OrdinalIgnoreCase)
            ? new StartupTiming() : null;
        try
        {
            if (selfTest) RunSelfTest();
            else Run(timing);
        }
        catch (Exception exception)
        {
            if (selfTest)
            {
                Console.Error.WriteLine(exception);
                Environment.ExitCode = 1;
            }
            else
                MessageBox.Show(exception.ToString(), "Application Foundry startup error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void Run(StartupTiming? timing)
    {
        ApplicationConfiguration.Initialize();
        timing?.Mark("windows forms initialized");

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ApplicationFoundry");
        Directory.CreateDirectory(dataDirectory);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "app.db"),
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var desktopExport = new DesktopDocumentExport();
        using var provider = CreateProvider(connectionString, desktopExport);
        timing?.Mark("services created");
        using (var scope = provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
        timing?.Mark("database migrated");

        Application.Run(new DesktopWindow(provider, desktopExport, timing is null ? null : timing.Mark));
        timing?.Mark("message loop ended");
    }

    private static void RunSelfTest()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"application-foundry-startup-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Pooling = false
            }.ToString();
            using var provider = CreateProvider(connectionString, new DesktopDocumentExport());
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
            scope.ServiceProvider.GetRequiredService<IJobDiscoveryService>();
            scope.ServiceProvider.GetRequiredService<GreenhouseJobDiscoveryService>();
            scope.ServiceProvider.GetRequiredService<LeverJobDiscoveryService>();
            scope.ServiceProvider.GetRequiredService<ArbeitnowJobDiscoveryService>();
            scope.ServiceProvider.GetRequiredService<ICareerDocumentService>();
            scope.ServiceProvider.GetRequiredService<ICandidateDocumentImportService>();
            scope.ServiceProvider.GetRequiredService<LegacyAccessCsvPreviewService>();
            scope.ServiceProvider.GetRequiredService<LegacyEvidenceImportService>();
            scope.ServiceProvider.GetRequiredService<LegacyProfileImportService>();
        }
        finally { File.Delete(databasePath); }
    }

    private static ServiceProvider CreateProvider(string connectionString, DesktopDocumentExport desktopExport)
    {
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
        services.AddCareerFeatures();
        services.AddSingleton<IClientDocumentExport>(desktopExport);
        return services.BuildServiceProvider();
    }
}

internal sealed class StartupTiming
{
    private readonly Stopwatch watch = Stopwatch.StartNew();
    private readonly string path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ApplicationFoundry", "startup-timing.txt");

    public StartupTiming()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var sinceProcessStart = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            File.WriteAllText(path, $"Process start to Main: {sinceProcessStart.TotalMilliseconds:F0} ms{Environment.NewLine}");
            Mark("main entered");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Mark(string milestone)
    {
        try { File.AppendAllText(path, $"{watch.ElapsedMilliseconds} ms: {milestone}{Environment.NewLine}"); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

internal sealed class DesktopWindow : Form
{
    public DesktopWindow(IServiceProvider services, DesktopDocumentExport desktopExport, Action<string>? mark = null)
    {
        mark?.Invoke("window constructor entered");
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
        view.BlazorWebViewInitializing += (_, _) => mark?.Invoke("webview initializing");
        view.BlazorWebViewInitialized += (_, _) => mark?.Invoke("webview initialized");
        Shown += (_, _) => mark?.Invoke("window shown");
        view.RootComponents.Add<Studio>("#app");
        Controls.Add(view);
        mark?.Invoke("window constructed");
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
