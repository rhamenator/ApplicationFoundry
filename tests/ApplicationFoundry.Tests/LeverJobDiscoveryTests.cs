using System.Net;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Fit;
using ApplicationFoundry.Features.Jobs;

namespace ApplicationFoundry.Tests;

public sealed class LeverJobDiscoveryTests
{
    [Theory]
    [InlineData("global", "api.lever.co", "jobs.lever.co")]
    [InlineData("eu", "api.eu.lever.co", "jobs.eu.lever.co")]
    public async Task FindsLocalMatchesWithoutSendingCandidateData(string region, string apiHost, string jobsHost)
    {
        var handler = new FeedHandler(jobsHost);
        var service = new LeverJobDiscoveryService(new HttpClient(handler), new TitleScorer());
        var profile = new CandidateProfile { FirstName = "Private", Summary = "C# developer" };

        var results = await service.FindMatchesAsync(profile, "example_site", "Example Employer",
            new JobSearchFilters("C#, software", .5f), "Detroit", region);

        var result = Assert.Single(results);
        Assert.Equal("C# Developer", result.RoleTitle);
        Assert.Equal("Example Employer", result.Organization);
        Assert.Equal("Detroit, MI", result.Location);
        Assert.Equal("Lever", result.SourceName);
        Assert.Contains("Build software", result.Description);
        Assert.Contains("Review code", result.Description);
        Assert.Equal($"https://{jobsHost}/example_site/2", result.SourceUrl);
        Assert.Equal($"https://{apiHost}/v0/postings/example_site?mode=json&limit=500", handler.RequestUri);
        Assert.False(handler.RequestHadBody);
        Assert.DoesNotContain("Private", handler.RequestUri);
    }

    [Fact]
    public async Task RejectsInvalidSiteOrRegionBeforeNetworkRequest()
    {
        var handler = new FeedHandler("jobs.lever.co");
        var service = new LeverJobDiscoveryService(new HttpClient(handler), new TitleScorer());

        await Assert.ThrowsAsync<ArgumentException>(() => service.FindMatchesAsync(
            new CandidateProfile(), "../other", "Example Employer"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.FindMatchesAsync(
            new CandidateProfile(), "example_site", "Example Employer", region: "other"));
        Assert.Null(handler.RequestUri);
    }

    private sealed class FeedHandler(string jobsHost) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public bool RequestHadBody { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.AbsoluteUri;
            RequestHadBody = request.Content is not null;
            var feed = $$"""
                [
                  {"text":"Office Assistant","categories":{"location":"Detroit, MI"},"hostedUrl":"https://{{jobsHost}}/example_site/1","descriptionPlain":"Manage records"},
                  {"text":"C# Developer","categories":{"location":"Detroit, MI"},"hostedUrl":"https://{{jobsHost}}/example_site/2","descriptionPlain":"Build software","lists":[{"text":"Responsibilities","content":"<li>Review code</li>"}]},
                  {"text":"C# Developer","categories":{"location":"Detroit, MI"},"hostedUrl":"https://{{jobsHost}}/example_site/2","descriptionPlain":"Build software"},
                  {"text":"C# Developer","categories":{"location":"Remote"},"hostedUrl":"https://{{jobsHost}}/example_site/3","descriptionPlain":"Build software"},
                  {"text":"C# Developer","categories":{"location":"Detroit, MI"},"hostedUrl":"https://elsewhere.example/example_site/4","descriptionPlain":"Build software"}
                ]
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(feed) });
        }
    }

    private sealed class TitleScorer : IFitScorer
    {
        public FitResult Score(CandidateProfile profile, JobOpportunity job) =>
            new(job.RoleTitle.StartsWith("C#", StringComparison.Ordinal) ? .9f : .1f, "local score", []);
    }
}
