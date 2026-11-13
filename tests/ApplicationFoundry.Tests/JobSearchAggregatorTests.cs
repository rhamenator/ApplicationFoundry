using ApplicationFoundry.Features.Jobs;

namespace ApplicationFoundry.Tests;

public sealed class JobSearchAggregatorTests
{
    [Fact]
    public void MergesRanksAndDeduplicatesCrossBoardResults()
    {
        var remote = new DiscoveredJob("Engineer", "Example", "https://jobs.example.test/roles/1?source=remote",
            "Synthetic posting", 0.55f, "Local fit", "Remote OK", "Remote");
        var duplicate = remote with { SourceUrl = "https://jobs.example.test/roles/1?source=employer", FitScore = 0.75f, SourceName = "Greenhouse" };
        var local = new DiscoveredJob("Analyst", "Local Example", "https://jobs.example.test/roles/2",
            "Synthetic local posting", 0.9f, "Local fit", "Lever", "Detroit");
        var invalid = remote with { SourceUrl = "http://jobs.example.test/roles/3" };

        var merged = JobSearchAggregator.Merge([[remote, invalid], [duplicate], [local]]);

        Assert.Equal(2, merged.Count);
        Assert.Equal("Analyst", merged[0].RoleTitle);
        Assert.Equal("Greenhouse", merged[1].SourceName);
        Assert.Equal(0.75f, merged[1].FitScore);
    }
}
