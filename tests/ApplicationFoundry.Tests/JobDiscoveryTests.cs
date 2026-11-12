using System.Net;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Fit;
using ApplicationFoundry.Features.Jobs;

namespace ApplicationFoundry.Tests;

public class JobDiscoveryTests
{
    [Fact]
    public async Task SearchRanksFeedLocallyAndDoesNotSendCandidateData()
    {
        var handler = new FeedHandler();
        var service = new RemoteJobDiscoveryService(new HttpClient(handler), new TitleScorer());
        var profile = new CandidateProfile { FirstName = "Private", LastName = "Applicant", Summary = "C# developer" };

        var results = await service.FindMatchesAsync(profile);

        Assert.Equal(2, results.Count);
        Assert.Equal("C# Developer", results[0].RoleTitle);
        Assert.Equal("https://remoteok.com/remote-jobs/2", results[0].SourceUrl);
        Assert.Equal("Build software", results[0].Description);
        Assert.Equal("https://remoteok.com/api", handler.RequestUri);
        Assert.False(handler.RequestHadBody);
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
                [
                  {"legal":"feed metadata"},
                  {"position":"Office Assistant","company":"Example","url":"https://remoteok.com/remote-jobs/1","description":"<p>Manage records</p>"},
                  {"position":"C# Developer","company":"Example","url":"https://remoteok.com/remote-jobs/2","description":"<p>Build software</p>"},
                  {"position":"Unsafe","company":"Example","url":"https://elsewhere.example/job","description":"<p>Ignore</p>"}
                ]
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed)
            });
        }
    }

    private sealed class TitleScorer : IFitScorer
    {
        public FitResult Score(CandidateProfile profile, JobOpportunity job) =>
            new(job.RoleTitle.StartsWith("C#", StringComparison.Ordinal) ? .9f : .1f, "local score", []);
    }
}
