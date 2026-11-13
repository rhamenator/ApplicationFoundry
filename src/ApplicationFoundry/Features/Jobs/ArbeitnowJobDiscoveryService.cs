using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Fit;

namespace ApplicationFoundry.Features.Jobs;

public sealed partial class ArbeitnowJobDiscoveryService(HttpClient client, IFitScorer scorer)
{
    private static readonly Uri EuropeFeed = new("https://www.arbeitnow.com/api/job-board-api");
    private static readonly Uri UkFeed = new("https://www.arbeitnow.co.uk/api/job-board-api");
    private static readonly HashSet<string> PostingHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "www.arbeitnow.com", "www.arbeitnow.co.uk", "www.arbeitnow.fr",
        "www.arbeitnow.ch", "www.arbeitnow.nl"
    };

    public async Task<IReadOnlyList<DiscoveredJob>> FindMatchesAsync(
        CandidateProfile profile, JobSearchFilters? filters = null, string location = "",
        string region = "europe", CancellationToken cancellationToken = default)
    {
        var feedUrl = region switch
        {
            "europe" => EuropeFeed,
            "uk" => UkFeed,
            _ => throw new ArgumentException("Choose Europe or UK for Arbeitnow.", nameof(region))
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
        request.Headers.UserAgent.ParseAdd("ApplicationFoundry/0.1 (local career studio)");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var feed = await JobFeedReader.ReadAsync(response.Content, cancellationToken);
        if (feed.RootElement.ValueKind != JsonValueKind.Object
            || !feed.RootElement.TryGetProperty("data", out var jobs)
            || jobs.ValueKind != JsonValueKind.Array)
            return [];

        var keywords = (filters?.Keywords ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var minimumFitScore = Math.Clamp(filters?.MinimumFitScore ?? 0, 0, 1);
        var matches = new List<DiscoveredJob>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in jobs.EnumerateArray().Take(500))
        {
            var title = ReadString(item, "title");
            var employer = ReadString(item, "company_name");
            var description = WebUtility.HtmlDecode(HtmlTags().Replace(ReadString(item, "description"), " ")).Trim();
            var jobLocation = ReadString(item, "location");
            var rawUrl = ReadString(item, "url");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(employer)
                || string.IsNullOrWhiteSpace(description)
                || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var url)
                || url.Scheme != Uri.UriSchemeHttps || !url.IsDefaultPort
                || !string.IsNullOrEmpty(url.UserInfo) || !PostingHosts.Contains(url.Host)
                || !url.AbsolutePath.StartsWith("/jobs/", StringComparison.Ordinal))
                continue;
            if (!string.IsNullOrWhiteSpace(location)
                && !jobLocation.Contains(location.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;
            if (keywords.Length > 0 && !keywords.All(keyword =>
                    title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || employer.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || description.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                continue;

            var opportunity = new JobOpportunity
            {
                RoleTitle = title[..Math.Min(title.Length, 200)],
                Organization = employer[..Math.Min(employer.Length, 200)],
                SourceUrl = url.AbsoluteUri,
                Description = description[..Math.Min(description.Length, 30000)]
            };
            var fit = scorer.Score(profile, opportunity);
            if (fit.Score < minimumFitScore || !seenUrls.Add(opportunity.SourceUrl)) continue;
            matches.Add(new DiscoveredJob(opportunity.RoleTitle, opportunity.Organization,
                opportunity.SourceUrl, opportunity.Description, fit.Score, fit.Explanation,
                "Arbeitnow", jobLocation));
        }
        return matches.OrderByDescending(job => job.FitScore).Take(12).ToArray();
    }

    private static string ReadString(JsonElement item, string property) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTags();
}
