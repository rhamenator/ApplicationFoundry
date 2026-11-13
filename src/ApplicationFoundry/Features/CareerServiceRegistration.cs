using ApplicationFoundry.Features.Documents;
using ApplicationFoundry.Features.Jobs;

namespace ApplicationFoundry.Features;

public static class CareerServiceRegistration
{
    public static IServiceCollection AddCareerFeatures(this IServiceCollection services)
    {
        services.AddHttpClient<IJobDiscoveryService, RemoteJobDiscoveryService>();
        services.AddHttpClient<GreenhouseJobDiscoveryService>();
        services.AddHttpClient<LeverJobDiscoveryService>();
        services.AddHttpClient<ArbeitnowJobDiscoveryService>();
        services.AddSingleton<ICareerDocumentService, CareerDocumentService>();
        services.AddSingleton<ICandidateDocumentImportService, CandidateDocumentImportService>();
        services.AddSingleton<LegacyAccessCsvPreviewService>();
        services.AddSingleton<LegacyEvidenceImportService>();
        services.AddSingleton<LegacyProfileImportService>();
        return services;
    }
}
