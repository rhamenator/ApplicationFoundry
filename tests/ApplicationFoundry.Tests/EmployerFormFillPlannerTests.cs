using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Forms;

namespace ApplicationFoundry.Tests;

public sealed class EmployerFormFillPlannerTests
{
    [Fact]
    public void Exact_reviewed_answers_generate_actions_for_synthetic_form()
    {
        var plan = EmployerFormFillPlanner.Prepare(Job(), new EmployerFormSnapshot(
            new Uri("https://example.test/apply"),
            [
                new EmployerFormField("applicant_email", "Email: *", "email", true),
                new EmployerFormField("availability", "Availability", "select", true, ["Two weeks", "Four weeks"])
            ]));
        Assert.True(plan.IsComplete);
        Assert.Equal(["applicant_email", "availability"], plan.Values.Select(action => action.Key));
        Assert.Equal("avery@example.test", plan.Values[0].Value);
        Assert.Empty(plan.Reasons);
    }

    [Fact]
    public void Missing_required_answer_blocks_all_actions()
    {
        var plan = EmployerFormFillPlanner.Prepare(Job(), new EmployerFormSnapshot(
            new Uri("https://example.test/apply"),
            [
                new EmployerFormField("email", "Email", "email", true),
                new EmployerFormField("citizenship", "Work authorization", "text", true)
            ]));
        Assert.False(plan.IsComplete);
        Assert.Empty(plan.Values);
        Assert.Contains(plan.Reasons, reason => reason.Contains("Work authorization", StringComparison.Ordinal));
    }

    [Fact]
    public void Different_origin_and_unsafe_required_field_block()
    {
        var plan = EmployerFormFillPlanner.Prepare(Job(), new EmployerFormSnapshot(
            new Uri("https://different.test/apply"),
            [
                new EmployerFormField("email", "Email", "email", true),
                new EmployerFormField("password", "Password", "password", true)
            ]));
        Assert.False(plan.IsComplete);
        Assert.Empty(plan.Values);
        Assert.Contains(plan.Reasons, reason => reason.Contains("same HTTPS origin", StringComparison.Ordinal));
        Assert.Contains(plan.Reasons, reason => reason.Contains("unsupported", StringComparison.Ordinal));
    }

    [Fact]
    public void Duplicate_field_key_and_unavailable_choice_block()
    {
        var plan = EmployerFormFillPlanner.Prepare(Job(), new EmployerFormSnapshot(
            new Uri("https://example.test/apply"),
            [
                new EmployerFormField("answer", "Email", "email", true),
                new EmployerFormField("answer", "Availability", "select", true, ["Immediately"])
            ]));
        Assert.False(plan.IsComplete);
        Assert.Empty(plan.Values);
        Assert.Contains(plan.Reasons, reason => reason.Contains("repeated", StringComparison.Ordinal));
    }

    [Fact]
    public void Unavailable_select_choice_blocks()
    {
        var plan = EmployerFormFillPlanner.Prepare(Job(), new EmployerFormSnapshot(
            new Uri("https://example.test/apply"),
            [new EmployerFormField("availability", "Availability", "select", true, ["Immediately"])]));
        Assert.False(plan.IsComplete);
        Assert.Empty(plan.Values);
        Assert.Contains(plan.Reasons, reason => reason.Contains("not one unambiguous available choice", StringComparison.Ordinal));
    }

    [Fact]
    public void Repeated_select_label_blocks_before_browser_handoff()
    {
        var plan = EmployerFormFillPlanner.Prepare(Job(), new EmployerFormSnapshot(
            new Uri("https://example.test/apply"),
            [new EmployerFormField("availability", "Availability", "select", true, ["Four weeks", "four WEEKS"])]));
        Assert.False(plan.IsComplete);
        Assert.Empty(plan.Values);
        Assert.Contains(plan.Reasons, reason => reason.Contains("unambiguous", StringComparison.Ordinal));
    }

    [Fact]
    public void Empty_required_reviewed_answer_blocks_before_browser_handoff()
    {
        var job = Job();
        job.FormPlan!.Answers[0].Answer = " ";
        var plan = EmployerFormFillPlanner.Prepare(job, new EmployerFormSnapshot(
            new Uri("https://example.test/apply"), [new EmployerFormField("email", "Email", "email", true)]));
        Assert.False(plan.IsComplete);
        Assert.Empty(plan.Values);
        Assert.Contains(plan.Reasons, reason => reason.Contains("empty reviewed answer", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://portfolio.example.test/avery", true)]
    [InlineData("http://portfolio.example.test/avery", false)]
    [InlineData("https://user:secret@portfolio.example.test/avery", false)]
    [InlineData("not a URL", false)]
    public void Reviewed_portfolio_url_is_validated_before_browser_handoff(string answer, bool expected)
    {
        var job = Job();
        job.FormPlan!.Answers.Add(new ApplicationFormAnswer
        {
            FieldLabel = "Portfolio",
            Answer = answer,
            Source = "Reviewed synthetic profile"
        });
        var plan = EmployerFormFillPlanner.Prepare(job, new EmployerFormSnapshot(
            new Uri("https://example.test/apply"), [new EmployerFormField("portfolio", "Portfolio", "url", true)]));
        Assert.Equal(expected, plan.IsComplete);
        if (expected) Assert.Equal(answer, Assert.Single(plan.Values).Value);
        else Assert.Empty(plan.Values);
    }

    [Fact]
    public void Repeated_visible_label_blocks_ambiguous_fill()
    {
        var plan = EmployerFormFillPlanner.Prepare(Job(), new EmployerFormSnapshot(
            new Uri("https://example.test/apply"),
            [
                new EmployerFormField("email-primary", "Email", "email", true),
                new EmployerFormField("email-secondary", "Email *", "email", true)
            ]));
        Assert.False(plan.IsComplete);
        Assert.Empty(plan.Values);
        Assert.Contains(plan.Reasons, reason => reason.Contains("repeated on the form", StringComparison.Ordinal));
    }

    private static JobOpportunity Job() => new()
    {
        RoleTitle = "Synthetic engineer",
        Organization = "Example company",
        SourceUrl = "https://example.test/job",
        FormPlan = new ApplicationFormPlan
        {
            Answers =
            [
                new ApplicationFormAnswer { FieldLabel = "Email", Answer = "avery@example.test", Source = "Reviewed profile" },
                new ApplicationFormAnswer { FieldLabel = "Availability", Answer = "Four weeks", Source = "Reviewed applicant statement" }
            ]
        }
    };
}
