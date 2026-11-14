namespace ApplicationFoundry.Data;

public sealed record AutomaticRuleResult(bool MatchesLocalRules, IReadOnlyList<string> Reasons);

public static class AutomaticSubmissionRuleEvaluator
{
    public static AutomaticRuleResult Evaluate(JobOpportunity job)
    {
        var reasons = new List<string>();
        if (job.FormPlan?.Mode != FormSubmissionMode.AutomaticWithinRules)
            reasons.Add("Automatic submission preference is not saved for this opportunity.");
        if (job.Status is OpportunityStatus.Submitted or OpportunityStatus.Interviewing or OpportunityStatus.Closed)
            reasons.Add("This opportunity is no longer in a pre-submission state.");
        if (!Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
            reasons.Add("A valid HTTPS employer posting URL is required.");

        string? allowedOrganization = null;
        int? minimumFit = null;
        foreach (var line in (job.FormPlan?.Rules ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[1].Length == 0)
            {
                reasons.Add($"Rule '{line}' is incomplete. Use organization=... or minimum-fit=...");
                continue;
            }
            switch (parts[0].ToLowerInvariant())
            {
                case "organization" when allowedOrganization is null:
                    allowedOrganization = parts[1];
                    break;
                case "minimum-fit" when minimumFit is null && int.TryParse(parts[1], out var value) && value is >= 0 and <= 100:
                    minimumFit = value;
                    break;
                default:
                    reasons.Add($"Rule '{line}' is unsupported or repeated.");
                    break;
            }
        }
        if (allowedOrganization is null)
            reasons.Add("Add one organization= rule naming the allowed employer.");
        else if (!string.Equals(allowedOrganization.Trim(), job.Organization.Trim(), StringComparison.OrdinalIgnoreCase))
            reasons.Add("The employer does not match the allowed organization.");
        if (minimumFit is null)
            reasons.Add("Add one minimum-fit= rule with a percentage from 0 to 100.");
        else if (!float.IsFinite(job.FitScore) || job.FitScore is < 0 or > 1 || job.FitScore * 100 < minimumFit)
            reasons.Add("The local fit score is below the minimum-fit rule.");

        foreach (var kind in new[] { CareerDocumentKind.Resume, CareerDocumentKind.CoverLetter })
        {
            var latest = job.Documents.Where(document => document.Kind == kind)
                .OrderByDescending(document => document.Version).ThenByDescending(document => document.Id).FirstOrDefault();
            if (latest?.ApprovedAt is null)
                reasons.Add($"The latest {kind} version must be approved.");
        }
        return new AutomaticRuleResult(reasons.Count == 0, reasons);
    }
}
