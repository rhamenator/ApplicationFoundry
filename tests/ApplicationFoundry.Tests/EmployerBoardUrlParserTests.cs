using ApplicationFoundry.Features.Jobs;

namespace ApplicationFoundry.Tests;

public sealed class EmployerBoardUrlParserTests
{
    [Theory]
    [InlineData("https://boards.greenhouse.io/example/jobs/123?gh_src=sample", "greenhouse", "example", "global")]
    [InlineData("https://jobs.lever.co/example/123", "lever", "example", "global")]
    [InlineData("https://jobs.eu.lever.co/example", "lever", "example", "eu")]
    public void ReadsBoardFromOfficialPostingOrBoardUrl(string input, string source, string token, string region)
    {
        Assert.True(EmployerBoardUrlParser.TryParse(input, out var board));
        Assert.Equal(new EmployerBoard(source, token, region), board);
    }

    [Theory]
    [InlineData("http://jobs.lever.co/example")]
    [InlineData("https://jobs.lever.co.evil.test/example")]
    [InlineData("https://jobs.lever.co/")]
    [InlineData("https://jobs.lever.co/invalid%2Ftoken")]
    [InlineData("https://user:password@jobs.lever.co/example")]
    [InlineData("https://boards.greenhouse.io/example#fragment")]
    public void RejectsUntrustedOrAmbiguousUrl(string input)
    {
        Assert.False(EmployerBoardUrlParser.TryParse(input, out var board));
        Assert.Null(board);
    }
}
