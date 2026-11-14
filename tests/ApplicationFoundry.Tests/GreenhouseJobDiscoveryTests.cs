using System.Net;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Fit;
using ApplicationFoundry.Features.Jobs;

namespace ApplicationFoundry.Tests;

public class GreenhouseJobDiscoveryTests
{
    [Fact]
    public async Task FindsNonremoteJobsWithLocalFiltersAndNoCandidateDataInRequest()
    {
        var handler = new FeedHandler();
        var service = new GreenhouseJobDiscoveryService(new HttpClient(handler), new TitleScorer());
        var profile = new CandidateProfile { FirstName = "Private", Summary = "C# developer" };

        var results = await service.FindMatchesAsync(profile, "example_employer", "Example Employer",
            new JobSearchFilters("C#, software", .5f), "Detroit");

        var result = Assert.Single(results);
        Assert.Equal("C# Developer", result.RoleTitle);
        Assert.Equal("Example Employer", result.Organization);
        Assert.Equal("Detroit, MI", result.Location);
        Assert.Equal("Greenhouse", result.SourceName);
        Assert.Equal("Build software", result.Description);
        Assert.Equal("https://boards.greenhouse.io/example_employer/jobs/2", result.SourceUrl);
        Assert.Equal("https://boards-api.greenhouse.io/v1/boards/example_employer/jobs?content=true", handler.RequestUri);
        Assert.False(handler.RequestHadBody);
        Assert.DoesNotContain("Private", handler.RequestUri);
    }

    [Fact]
    public async Task RejectsInvalidBoardTokenBeforeNetworkRequest()
    {
        var handler = new FeedHandler();
        var service = new GreenhouseJobDiscoveryService(new HttpClient(handler), new TitleScorer());

        await Assert.ThrowsAsync<ArgumentException>(() => service.FindMatchesAsync(
            new CandidateProfile(), "../other", "Example Employer"));

        Assert.Null(handler.RequestUri);
    }

    private sealed class FeedHandler : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public bool RequestHadBody { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.AbsoluteUri;
            RequestHadBody = request.Content is not null;
            const string feed = """
                {"jobs":[
                  {"title":"Office Assistant","location":{"name":"Detroit, MI"},"absolute_url":"https://boards.greenhouse.io/example_employer/jobs/1","content":"<p>Manage records</p>"},
                  {"title":"C# Developer","location":{"name":"Detroit, MI"},"absolute_url":"https://boards.greenhouse.io/example_employer/jobs/2","content":"&lt;p&gt;Build software&lt;/p&gt;"},
                  {"title":"C# Developer","location":{"name":"Detroit, MI"},"absolute_url":"https://boards.greenhouse.io/example_employer/jobs/2","content":"<p>Build software</p>"},
                  {"title":"C# Developer","location":{"name":"Remote"},"absolute_url":"https://boards.greenhouse.io/example_employer/jobs/3","content":"<p>Build software</p>"},
                  {"title":"C# Developer","location":{"name":"Detroit, MI"},"absolute_url":"http://example.com/jobs/4","content":"<p>Build software</p>"}
                ]}
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
