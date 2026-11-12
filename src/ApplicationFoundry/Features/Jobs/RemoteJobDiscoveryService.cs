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
    string FitExplanation);

public interface IJobDiscoveryService
{
    Task<IReadOnlyList<DiscoveredJob>> FindMatchesAsync(CandidateProfile profile, CancellationToken cancellationToken = default);
}

public sealed partial class RemoteJobDiscoveryService(HttpClient client, IFitScorer scorer) : IJobDiscoveryService
{
    private static readonly Uri FeedUrl = new("https://remoteok.com/api");

    public async Task<IReadOnlyList<DiscoveredJob>> FindMatchesAsync(
        CandidateProfile profile, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
        request.Headers.UserAgent.ParseAdd("ApplicationFoundry/0.1 (local career studio)");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var feed = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var matches = new List<DiscoveredJob>();
        if (feed.RootElement.ValueKind != JsonValueKind.Array) return matches;

        foreach (var item in feed.RootElement.EnumerateArray())
        {
            if (matches.Count >= 100) break;
            var title = ReadString(item, "position");
            var organization = ReadString(item, "company");
            var rawUrl = ReadString(item, "url");
            var description = CleanDescription(ReadString(item, "description"));
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description)
                || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var url)
                || url.Scheme != Uri.UriSchemeHttps
                || url.Host is not ("remoteok.com" or "www.remoteok.com"))
                continue;

            var opportunity = new JobOpportunity
            {
                RoleTitle = title[..Math.Min(title.Length, 200)],
                Organization = organization[..Math.Min(organization.Length, 200)],
                SourceUrl = url.AbsoluteUri,
                Description = description[..Math.Min(description.Length, 30000)]
            };
            var fit = scorer.Score(profile, opportunity);
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
