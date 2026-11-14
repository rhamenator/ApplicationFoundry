namespace ApplicationFoundry.Features.Jobs;

public static class JobSearchAggregator
{
    public static IReadOnlyList<DiscoveredJob> Merge(IEnumerable<IReadOnlyList<DiscoveredJob>> sources)
    {
        var jobs = new Dictionary<string, DiscoveredJob>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in sources.SelectMany(source => source))
        {
            if (!Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var url)
                || url.Scheme != Uri.UriSchemeHttps) continue;
            var key = url.GetLeftPart(UriPartial.Path).TrimEnd('/');
            if (!jobs.TryGetValue(key, out var existing) || job.FitScore > existing.FitScore)
                jobs[key] = job;
        }
        return jobs.Values.OrderByDescending(job => job.FitScore)
            .ThenBy(job => job.Organization, StringComparer.OrdinalIgnoreCase)
            .Take(24).ToArray();
    }
}
