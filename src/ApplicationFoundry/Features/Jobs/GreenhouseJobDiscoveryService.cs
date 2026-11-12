using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Fit;

namespace ApplicationFoundry.Features.Jobs;

public sealed partial class GreenhouseJobDiscoveryService(HttpClient client, IFitScorer scorer)
{
    public async Task<IReadOnlyList<DiscoveredJob>> FindMatchesAsync(
        CandidateProfile profile, string boardToken, string employer,
        JobSearchFilters? filters = null, string location = "", CancellationToken cancellationToken = default)
    {
        boardToken = boardToken.Trim();
        employer = employer.Trim();
        if (!BoardTokenPattern().IsMatch(boardToken))
            throw new ArgumentException("Enter a valid Greenhouse board token (letters, numbers, hyphens, or underscores).", nameof(boardToken));
        if (string.IsNullOrWhiteSpace(employer) || employer.Length > 200)
            throw new ArgumentException("Enter the employer name shown on the board.", nameof(employer));

        var feedUrl = new Uri($"https://boards-api.greenhouse.io/v1/boards/{boardToken}/jobs?content=true");
        using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
        request.Headers.UserAgent.ParseAdd("ApplicationFoundry/0.1 (local career studio)");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var feed = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (feed.RootElement.ValueKind != JsonValueKind.Object
            || !feed.RootElement.TryGetProperty("jobs", out var jobs)
            || jobs.ValueKind != JsonValueKind.Array)
            return [];

        var keywords = (filters?.Keywords ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var minimumFitScore = Math.Clamp(filters?.MinimumFitScore ?? 0, 0, 1);
        var matches = new List<DiscoveredJob>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in jobs.EnumerateArray().Take(500))
        {
            var title = ReadString(item, "title");
            var rawUrl = ReadString(item, "absolute_url");
            var description = CleanDescription(ReadString(item, "content"));
            var jobLocation = item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("location", out var locationValue)
                ? ReadString(locationValue, "name") : "";
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description)
                || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var url)
                || url.Scheme != Uri.UriSchemeHttps || url.IsLoopback)
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
                Organization = employer,
                SourceUrl = url.AbsoluteUri,
                Description = description[..Math.Min(description.Length, 30000)]
            };
            var fit = scorer.Score(profile, opportunity);
            if (fit.Score < minimumFitScore || !seenUrls.Add(opportunity.SourceUrl)) continue;
            matches.Add(new DiscoveredJob(opportunity.RoleTitle, opportunity.Organization,
                opportunity.SourceUrl, opportunity.Description, fit.Score, fit.Explanation,
                "Greenhouse", jobLocation));
        }

        return matches.OrderByDescending(job => job.FitScore).Take(12).ToArray();
    }

    private static string ReadString(JsonElement item, string property) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static string CleanDescription(string html) =>
        WebUtility.HtmlDecode(HtmlTags().Replace(WebUtility.HtmlDecode(html), " ")).Trim();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex BoardTokenPattern();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTags();
}
