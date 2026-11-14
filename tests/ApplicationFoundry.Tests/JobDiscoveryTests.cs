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

    [Fact]
    public async Task SearchFiltersLocallyAndRemovesTrackingUrlDuplicates()
    {
        var handler = new FeedHandler();
        var service = new RemoteJobDiscoveryService(new HttpClient(handler), new TitleScorer());
        var profile = new CandidateProfile { FirstName = "Private", Summary = "C# developer" };

        var results = await service.FindMatchesAsync(profile, new JobSearchFilters("C#, software", .5f));

        var result = Assert.Single(results);
        Assert.Equal("C# Developer", result.RoleTitle);
        Assert.Equal("https://remoteok.com/remote-jobs/2", result.SourceUrl);
        Assert.Equal("https://remoteok.com/api", handler.RequestUri);
        Assert.False(handler.RequestHadBody);
    }

    [Fact]
    public async Task MinimumFitExcludesLowerRankedPostings()
    {
        var service = new RemoteJobDiscoveryService(new HttpClient(new FeedHandler()), new TitleScorer());

        var results = await service.FindMatchesAsync(new CandidateProfile(), new JobSearchFilters(MinimumFitScore: .5f));

        Assert.Single(results);
        Assert.Equal("C# Developer", results[0].RoleTitle);
    }

    [Fact]
    public async Task RejectsOversizedJobFeedBeforeParsing()
    {
        var service = new RemoteJobDiscoveryService(new HttpClient(new FeedHandler(oversized: true)), new TitleScorer());

        await Assert.ThrowsAsync<InvalidDataException>(() => service.FindMatchesAsync(new CandidateProfile()));
    }

    [Fact]
    public async Task RejectsOversizedChunkedJobFeed()
    {
        var service = new RemoteJobDiscoveryService(new HttpClient(new FeedHandler(chunkedOversized: true)), new TitleScorer());

        await Assert.ThrowsAsync<InvalidDataException>(() => service.FindMatchesAsync(new CandidateProfile()));
    }

    private sealed class FeedHandler(bool oversized = false, bool chunkedOversized = false) : HttpMessageHandler
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
                  {"position":"C# Developer","company":"Example","url":"https://www.remoteok.com/remote-jobs/2?ref=feed","description":"<p>Build software</p>"},
                  {"position":"Unsafe","company":"Example","url":"https://elsewhere.example/job","description":"<p>Ignore</p>"}
                ]
                """;
            HttpContent content = chunkedOversized
                ? new StreamContent(new SyntheticFeedStream(16 * 1024 * 1024 + 1))
                : new StringContent(feed);
            if (oversized) content.Headers.ContentLength = 16 * 1024 * 1024 + 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
        }
    }

    private sealed class SyntheticFeedStream(long remaining) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(remaining, count);
            Array.Fill(buffer, (byte)' ', offset, read);
            remaining -= read;
            return read;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = (int)Math.Min(remaining, buffer.Length);
            buffer[..read].Span.Fill((byte)' ');
            remaining -= read;
            return ValueTask.FromResult(read);
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class TitleScorer : IFitScorer
    {
        public FitResult Score(CandidateProfile profile, JobOpportunity job) =>
            new(job.RoleTitle.StartsWith("C#", StringComparison.Ordinal) ? .9f : .1f, "local score", []);
    }
}
