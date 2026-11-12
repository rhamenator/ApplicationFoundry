using System.Text.Json;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Forms;

namespace ApplicationFoundry.Tests;

public sealed class BrowserFormFillPacketBuilderTests
{
    [Fact]
    public void Only_current_authorization_produces_a_fill_packet()
    {
        var job = new JobOpportunity
        {
            Id = 42,
            SourceUrl = "https://example.test/job",
            FormPlan = new ApplicationFormPlan
            {
                Answers = [new ApplicationFormAnswer { FieldLabel = "Email", Answer = "avery@example.test", Source = "Reviewed profile" }]
            }
        };
        var form = new EmployerFormSnapshot(new Uri("https://example.test/apply"),
            [new EmployerFormField("email", "Email", "email", true)]);

        Assert.Null(BrowserFormFillPacketBuilder.Create(job, form, null));
        var authorization = FormFillAuthorizer.Authorize(job, form);
        var packet = BrowserFormFillPacketBuilder.Create(job, form, authorization);
        Assert.NotNull(packet);
        using (var json = JsonDocument.Parse(BrowserFormFillPacketBuilder.ToJson(packet)))
        {
            Assert.Equal("https://example.test/apply", json.RootElement.GetProperty("url").GetString());
            Assert.Equal("avery@example.test", json.RootElement.GetProperty("values")[0].GetProperty("value").GetString());
            Assert.False(json.RootElement.GetProperty("fields")[0].TryGetProperty("options", out _));
        }

        job.FormPlan.Answers[0].Answer = "changed@example.test";
        Assert.Null(BrowserFormFillPacketBuilder.Create(job, form, authorization));
        job.FormPlan.Answers[0].Answer = "avery@example.test";
        Assert.Null(BrowserFormFillPacketBuilder.Create(job,
            form with { Url = new Uri("https://other.test/apply") }, authorization));
        Assert.Null(BrowserFormFillPacketBuilder.Create(job, form,
            authorization! with { AuthorizedAt = DateTimeOffset.UtcNow - FormFillAuthorizer.MaxAge - TimeSpan.FromSeconds(1) }));
    }
}
