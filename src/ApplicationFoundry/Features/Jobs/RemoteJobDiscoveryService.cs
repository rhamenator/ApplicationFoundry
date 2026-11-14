using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Fit;

namespace ApplicationFoundry.Features.Jobs;

public sealed record DiscoveredJob(
    string RoleTitle,
    string Organization,
    string SourceUrl,
    string Description,
    float FitScore,
    string FitExplanation,
    string SourceName = "Remote OK",
    string Location = "Remote");

public sealed record JobSearchFilters(string Keywords = "", float MinimumFitScore = 0);

public interface IJobDiscoveryService
{
    Task<IReadOnlyList<DiscoveredJob>> FindMatchesAsync(CandidateProfile profile, JobSearchFilters? filters = null, CancellationToken cancellationToken = default);
}

public sealed partial class RemoteJobDiscoveryService(HttpClient client, IFitScorer scorer) : IJobDiscoveryService
{
    private static readonly Uri FeedUrl = new("https://remoteok.com/api");

    public async Task<IReadOnlyList<DiscoveredJob>> FindMatchesAsync(
        CandidateProfile profile, JobSearchFilters? filters = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
        request.Headers.UserAgent.ParseAdd("ApplicationFoundry/0.1 (local career studio)");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var feed = await JobFeedReader.ReadAsync(response.Content, cancellationToken);

        var matches = new List<DiscoveredJob>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keywords = (filters?.Keywords ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var minimumFitScore = Math.Clamp(filters?.MinimumFitScore ?? 0, 0, 1);
        if (feed.RootElement.ValueKind != JsonValueKind.Array) return matches;

        foreach (var item in feed.RootElement.EnumerateArray().Take(100))
        {
            var title = ReadString(item, "position");
            var organization = ReadString(item, "company");
            var rawUrl = ReadString(item, "url");
            var description = CleanDescription(ReadString(item, "description"));
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description)
                || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var url)
                || url.Scheme != Uri.UriSchemeHttps
                || url.Host is not ("remoteok.com" or "www.remoteok.com"))
                continue;

            var canonicalUrl = new UriBuilder(url) { Host = "remoteok.com", Query = "", Fragment = "" }.Uri.AbsoluteUri.TrimEnd('/');
            if (keywords.Length > 0 && !keywords.All(keyword =>
                    title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || organization.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || description.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                continue;

            var opportunity = new JobOpportunity
            {
                RoleTitle = title[..Math.Min(title.Length, 200)],
                Organization = organization[..Math.Min(organization.Length, 200)],
                SourceUrl = canonicalUrl,
                Description = description[..Math.Min(description.Length, 30000)]
            };
            var fit = scorer.Score(profile, opportunity);
            if (fit.Score < minimumFitScore || !seenUrls.Add(canonicalUrl)) continue;
            matches.Add(new DiscoveredJob(opportunity.RoleTitle, opportunity.Organization,
                opportunity.SourceUrl, opportunity.Description, fit.Score, fit.Explanation));
        }

        return matches.OrderByDescending(job => job.FitScore).Take(12).ToArray();
    }

    private static string ReadString(JsonElement item, string property) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static string CleanDescription(string html) =>
        WebUtility.HtmlDecode(HtmlTags().Replace(html, " ")).Trim();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTags();
}
