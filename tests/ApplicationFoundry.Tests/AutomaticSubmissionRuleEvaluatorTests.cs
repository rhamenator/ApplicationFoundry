using ApplicationFoundry.Data;

namespace ApplicationFoundry.Tests;

public sealed class AutomaticSubmissionRuleEvaluatorTests
{
    [Fact]
    public void Exact_employer_and_fit_with_approved_latest_documents_match_locally()
    {
        var job = SyntheticJob();
        var result = AutomaticSubmissionRuleEvaluator.Evaluate(job);
        Assert.True(result.MatchesLocalRules);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Unknown_rules_and_changed_employer_fail_closed()
    {
        var job = SyntheticJob();
        job.Organization = "Different employer";
        job.FormPlan!.Rules += "\nauto-submit-everywhere=true";
        var result = AutomaticSubmissionRuleEvaluator.Evaluate(job);
        Assert.False(result.MatchesLocalRules);
        Assert.Contains(result.Reasons, reason => reason.Contains("unsupported", StringComparison.Ordinal));
        Assert.Contains(result.Reasons, reason => reason.Contains("does not match", StringComparison.Ordinal));
    }

    [Fact]
    public void Low_fit_or_new_unapproved_document_blocks()
    {
        var job = SyntheticJob();
        job.FitScore = 0.49f;
        job.Documents.Add(new DocumentVersion { Id = 3, Kind = CareerDocumentKind.Resume, Version = 2, Content = "New synthetic version" });
        var result = AutomaticSubmissionRuleEvaluator.Evaluate(job);
        Assert.False(result.MatchesLocalRules);
        Assert.Contains(result.Reasons, reason => reason.Contains("below", StringComparison.Ordinal));
        Assert.Contains(result.Reasons, reason => reason.Contains("Resume", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_or_duplicate_rules_and_insecure_url_block()
    {
        var job = SyntheticJob();
        job.FormPlan!.Rules = "organization=Example employer\norganization=Example employer";
        job.SourceUrl = "http://example.test/job";
        var result = AutomaticSubmissionRuleEvaluator.Evaluate(job);
        Assert.False(result.MatchesLocalRules);
        Assert.Contains(result.Reasons, reason => reason.Contains("repeated", StringComparison.Ordinal));
        Assert.Contains(result.Reasons, reason => reason.Contains("minimum-fit", StringComparison.Ordinal));
        Assert.Contains(result.Reasons, reason => reason.Contains("HTTPS", StringComparison.Ordinal));
    }

    [Fact]
    public void Invalid_score_and_post_submission_status_block()
    {
        var job = SyntheticJob();
        job.FitScore = float.NaN;
        job.Status = OpportunityStatus.Submitted;
        var result = AutomaticSubmissionRuleEvaluator.Evaluate(job);
        Assert.False(result.MatchesLocalRules);
        Assert.Contains(result.Reasons, reason => reason.Contains("pre-submission", StringComparison.Ordinal));
        Assert.Contains(result.Reasons, reason => reason.Contains("fit score", StringComparison.Ordinal));
    }

    private static JobOpportunity SyntheticJob() => new()
    {
        RoleTitle = "Synthetic analyst",
        Organization = "Example employer",
        SourceUrl = "https://example.test/job",
        FitScore = 0.75f,
        FormPlan = new ApplicationFormPlan
        {
            Mode = FormSubmissionMode.AutomaticWithinRules,
            Rules = "organization=Example employer\nminimum-fit=50"
        },
        Documents =
        [
            new DocumentVersion { Id = 1, Kind = CareerDocumentKind.Resume, Version = 1, Content = "Synthetic resume", ApprovedAt = DateTimeOffset.UtcNow },
            new DocumentVersion { Id = 2, Kind = CareerDocumentKind.CoverLetter, Version = 1, Content = "Synthetic letter", ApprovedAt = DateTimeOffset.UtcNow }
        ]
    };
}
