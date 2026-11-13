using System.Net;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Fit;
using ApplicationFoundry.Features.Jobs;

namespace ApplicationFoundry.Tests;

public sealed class ArbeitnowJobDiscoveryTests
{
    [Theory]
    [InlineData("europe", "https://www.arbeitnow.com/api/job-board-api", "https://www.arbeitnow.com/jobs/companies/example/analyst")]
    [InlineData("uk", "https://www.arbeitnow.co.uk/api/job-board-api", "https://www.arbeitnow.co.uk/jobs/companies/example/analyst")]
    public async Task FindsLocalJobsWithoutSendingProfile(string region, string feedUrl, string postingUrl)
    {
        var handler = new FeedHandler(postingUrl);
        var service = new ArbeitnowJobDiscoveryService(new HttpClient(handler), new SyntheticScorer());
        var results = await service.FindMatchesAsync(new CandidateProfile { FirstName = "Private" },
            new JobSearchFilters("analyst", .5f), "London", region);

        var result = Assert.Single(results);
        Assert.Equal("Analyst", result.RoleTitle);
        Assert.Equal("Example", result.Organization);
        Assert.Equal("London", result.Location);
        Assert.Equal("Arbeitnow", result.SourceName);
        Assert.Equal("Build reports & dashboards", result.Description);
        Assert.Equal(feedUrl, handler.RequestUri);
        Assert.False(handler.RequestHadBody);
        Assert.DoesNotContain("Private", handler.RequestUri);
    }

    [Fact]
    public async Task RejectsUnsupportedRegionBeforeRequest()
    {
        var handler = new FeedHandler("https://www.arbeitnow.com/jobs/companies/example/analyst");
        var service = new ArbeitnowJobDiscoveryService(new HttpClient(handler), new SyntheticScorer());
        await Assert.ThrowsAsync<ArgumentException>(() => service.FindMatchesAsync(new CandidateProfile(), region: "other"));
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task EuropeFeedAcceptsOtherOfficialCountryPostings()
    {
        var handler = new FeedHandler("https://www.arbeitnow.fr/jobs/companies/example/analyst");
        var service = new ArbeitnowJobDiscoveryService(new HttpClient(handler), new SyntheticScorer());
        var result = Assert.Single(await service.FindMatchesAsync(new CandidateProfile(),
            new JobSearchFilters("analyst", .5f), "London"));
        Assert.Equal("https://www.arbeitnow.fr/jobs/companies/example/analyst", result.SourceUrl);
    }

    private sealed class FeedHandler(string postingUrl) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public bool RequestHadBody { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.AbsoluteUri;
            RequestHadBody = request.Content is not null;
            var feed = $$"""
                {"data":[
                  {"title":"Analyst","company_name":"Example","location":"London","description":"<p>Build reports &amp; dashboards</p>","url":"{{postingUrl}}"},
                  {"title":"Analyst","company_name":"Example","location":"London","description":"<p>Build reports &amp; dashboards</p>","url":"{{postingUrl}}?source=feed"},
                  {"title":"Analyst","company_name":"Example","location":"Paris","description":"<p>Build reports</p>","url":"{{postingUrl}}"},
                  {"title":"Analyst","company_name":"Example","location":"London","description":"<p>Build reports</p>","url":"https://evil.test/jobs/analyst"}
                ]}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(feed) });
        }
    }

    private sealed class SyntheticScorer : IFitScorer
    {
        public FitResult Score(CandidateProfile profile, JobOpportunity job) => new(.9f, "local score", []);
    }
}
