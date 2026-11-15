using ApplicationFoundry.Data;
using ApplicationFoundry.Features;
using ApplicationFoundry.Features.Documents;
using ApplicationFoundry.Features.Fit;
using ApplicationFoundry.Features.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApplicationFoundry.Tests;

public sealed class CareerServiceRegistrationTests
{
    [Fact]
    public void SharedRegistrationResolvesStudioCareerServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        services.AddSingleton<IFitScorer, ManagedFitScorer>();
        services.AddCareerFeatures();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<IJobDiscoveryService>());
        Assert.NotNull(provider.GetRequiredService<GreenhouseJobDiscoveryService>());
        Assert.NotNull(provider.GetRequiredService<LeverJobDiscoveryService>());
        Assert.NotNull(provider.GetRequiredService<ArbeitnowJobDiscoveryService>());
        Assert.NotNull(provider.GetRequiredService<ICareerDocumentService>());
        Assert.NotNull(provider.GetRequiredService<ICandidateDocumentImportService>());
        Assert.NotNull(provider.GetRequiredService<LegacyAccessCsvPreviewService>());
        Assert.NotNull(provider.GetRequiredService<LegacyEvidenceImportService>());
        Assert.NotNull(provider.GetRequiredService<LegacyProfileImportService>());
    }
}
