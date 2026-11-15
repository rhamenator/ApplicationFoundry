using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Forms;

namespace ApplicationFoundry.Tests;

public sealed class FormFillAuthorizerTests
{
    [Fact]
    public void Authorization_is_bound_to_exact_reviewed_value_and_form()
    {
        var job = Job();
        var form = Form();
        var authorization = FormFillAuthorizer.Authorize(job, form);

        Assert.NotNull(authorization);
        Assert.True(FormFillAuthorizer.IsCurrent(job, form, authorization));

        job.FormPlan!.Answers[0].Answer = "changed@example.test";
        Assert.False(FormFillAuthorizer.IsCurrent(job, form, authorization));
        job.FormPlan.Answers[0].Answer = "avery@example.test";

        Assert.False(FormFillAuthorizer.IsCurrent(job,
            form with { Fields = [new EmployerFormField("different-key", "Email", "email", true)] },
            authorization));
        Assert.False(FormFillAuthorizer.IsCurrent(job,
            form with { Url = new Uri("https://example.test/another-form") }, authorization));

        job.FormPlan.Rules = "minimum-fit=75";
        Assert.False(FormFillAuthorizer.IsCurrent(job, form, authorization));
    }

    [Fact]
    public void Authorization_expires_and_invalid_packets_fail_closed()
    {
        var job = Job();
        var form = Form();
        var authorization = FormFillAuthorizer.Authorize(job, form)!;

        Assert.False(FormFillAuthorizer.IsCurrent(job, form,
            authorization with { AuthorizedAt = DateTimeOffset.UtcNow - FormFillAuthorizer.MaxAge - TimeSpan.FromSeconds(1) }));
        Assert.False(FormFillAuthorizer.IsCurrent(job, form, authorization with { Fingerprint = "invalid" }));

        job.FormPlan!.Answers.Clear();
        Assert.Null(FormFillAuthorizer.Authorize(job, form));
        Assert.False(FormFillAuthorizer.IsCurrent(job, form, authorization));
    }

    [Fact]
    public void External_form_requires_exact_applicant_confirmed_url_and_reauthorization_after_change()
    {
        var job = Job();
        var externalUrl = new Uri("https://apply.example.test/form?job=42");
        var form = Form() with { Url = externalUrl };
        Assert.Null(FormFillAuthorizer.Authorize(job, form));

        var confirmed = form with { ConfirmedExternalUrl = externalUrl };
        var authorization = FormFillAuthorizer.Authorize(job, confirmed);
        Assert.NotNull(authorization);
        Assert.NotNull(BrowserFormFillPacketBuilder.Create(job, confirmed, authorization));
        Assert.False(FormFillAuthorizer.IsCurrent(job, form, authorization));
        Assert.False(FormFillAuthorizer.IsCurrent(job,
            confirmed with { ConfirmedExternalUrl = new Uri("https://apply.example.test/form?job=other") }, authorization));
        Assert.Null(FormFillAuthorizer.Authorize(job,
            form with { ConfirmedExternalUrl = new Uri("http://apply.example.test/form?job=42") }));
        Assert.Null(FormFillAuthorizer.Authorize(job,
            form with { ConfirmedExternalUrl = new Uri("https://user:secret@apply.example.test/form?job=42") }));
    }

    private static JobOpportunity Job() => new()
    {
        Id = 42,
        Organization = "Example company",
        SourceUrl = "https://example.test/job",
        FormPlan = new ApplicationFormPlan
        {
            Answers = [new ApplicationFormAnswer { FieldLabel = "Email", Answer = "avery@example.test", Source = "Reviewed profile" }]
        }
    };

    private static EmployerFormSnapshot Form() => new(
        new Uri("https://example.test/apply"),
        [new EmployerFormField("email", "Email", "email", true)]);
}
