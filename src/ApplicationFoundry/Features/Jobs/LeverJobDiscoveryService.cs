using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Fit;

namespace ApplicationFoundry.Features.Jobs;

public sealed partial class LeverJobDiscoveryService(HttpClient client, IFitScorer scorer)
{
    public async Task<IReadOnlyList<DiscoveredJob>> FindMatchesAsync(
        CandidateProfile profile, string siteToken, string employer,
        JobSearchFilters? filters = null, string location = "", string region = "global",
        CancellationToken cancellationToken = default)
    {
        siteToken = siteToken.Trim();
        employer = employer.Trim();
        if (!SiteTokenPattern().IsMatch(siteToken))
            throw new ArgumentException("Enter a valid Lever site token (letters, numbers, hyphens, or underscores).", nameof(siteToken));
        if (string.IsNullOrWhiteSpace(employer) || employer.Length > 200)
            throw new ArgumentException("Enter the employer name shown on the site.", nameof(employer));
        if (region is not ("global" or "eu"))
            throw new ArgumentException("Choose the global or EU Lever site.", nameof(region));

        var apiHost = region == "eu" ? "api.eu.lever.co" : "api.lever.co";
        var jobsHost = region == "eu" ? "jobs.eu.lever.co" : "jobs.lever.co";
        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri($"https://{apiHost}/v0/postings/{siteToken}?mode=json&limit=500"));
        request.Headers.UserAgent.ParseAdd("ApplicationFoundry/0.1 (local career studio)");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var feed = await JobFeedReader.ReadAsync(response.Content, cancellationToken);
        if (feed.RootElement.ValueKind != JsonValueKind.Array) return [];

        var keywords = (filters?.Keywords ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var minimumFitScore = Math.Clamp(filters?.MinimumFitScore ?? 0, 0, 1);
        var matches = new List<DiscoveredJob>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in feed.RootElement.EnumerateArray().Take(500))
        {
            var title = ReadString(item, "text");
            var rawUrl = ReadString(item, "hostedUrl");
            var description = ReadString(item, "descriptionPlain");
            if (string.IsNullOrWhiteSpace(description))
                description = CleanHtml(ReadString(item, "description"));
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("lists", out var lists)
                && lists.ValueKind == JsonValueKind.Array)
            {
                foreach (var list in lists.EnumerateArray().Take(20))
                    description += "\n" + ReadString(list, "text") + " " + CleanHtml(ReadString(list, "content"));
            }
            var jobLocation = item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("categories", out var categories)
                ? ReadString(categories, "location") : "";
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description)
                || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var url)
                || url.Scheme != Uri.UriSchemeHttps || !url.Host.Equals(jobsHost, StringComparison.OrdinalIgnoreCase)
                || !url.AbsolutePath.StartsWith($"/{siteToken}/", StringComparison.OrdinalIgnoreCase))
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
                Description = description.Trim()[..Math.Min(description.Trim().Length, 30000)]
            };
            var fit = scorer.Score(profile, opportunity);
            if (fit.Score < minimumFitScore || !seenUrls.Add(opportunity.SourceUrl)) continue;
            matches.Add(new DiscoveredJob(opportunity.RoleTitle, opportunity.Organization,
                opportunity.SourceUrl, opportunity.Description, fit.Score, fit.Explanation,
                "Lever", jobLocation));
        }
        return matches.OrderByDescending(job => job.FitScore).Take(12).ToArray();
    }

    private static string ReadString(JsonElement item, string property) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static string CleanHtml(string html) =>
        WebUtility.HtmlDecode(HtmlTags().Replace(WebUtility.HtmlDecode(html), " ")).Trim();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex SiteTokenPattern();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTags();
}
