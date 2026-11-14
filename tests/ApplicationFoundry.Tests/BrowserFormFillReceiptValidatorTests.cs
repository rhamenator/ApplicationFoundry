using System.Text.Json;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Forms;

namespace ApplicationFoundry.Tests;

public sealed class BrowserFormFillReceiptValidatorTests
{
    [Fact]
    public void Exact_browser_observation_matches_current_approved_packet()
    {
        var packet = Packet();
        var receipt = Receipt(packet, "avery@example.test", "Two weeks");

        Assert.True(BrowserFormFillReceiptValidator.TryMatch(receipt, packet, out var entered, out var error), error);
        Assert.Equal(2, entered.Count);
        Assert.Equal("Two weeks", entered[1].Value);
    }

    [Fact]
    public void Changed_value_or_origin_or_extra_data_blocks_receipt()
    {
        var packet = Packet();
        Assert.False(BrowserFormFillReceiptValidator.TryMatch(
            Receipt(packet, "changed@example.test", "Two weeks"), packet, out _, out _));
        Assert.False(BrowserFormFillReceiptValidator.TryMatch(
            Receipt(packet, "avery@example.test", "Two weeks").Replace(packet.Url, "https://different.test/apply"),
            packet, out _, out _));
        Assert.False(BrowserFormFillReceiptValidator.TryMatch(
            Receipt(packet, "avery@example.test", "Two weeks").Replace("\"values\":", "\"unexpected\":true,\"values\":"),
            packet, out _, out _));
    }

    private static BrowserFormFillPacket Packet()
    {
        var job = new JobOpportunity
        {
            Id = 42,
            SourceUrl = "https://example.test/job",
            FormPlan = new ApplicationFormPlan
            {
                Answers =
                [
                    new ApplicationFormAnswer { FieldLabel = "Email", Answer = "avery@example.test", Source = "Reviewed profile" },
                    new ApplicationFormAnswer { FieldLabel = "Availability", Answer = "Two weeks", Source = "Reviewed applicant statement" }
                ]
            }
        };
        var form = new EmployerFormSnapshot(new Uri("https://example.test/apply"),
        [
            new EmployerFormField("email", "Email", "email", true),
            new EmployerFormField("availability", "Availability", "select", true, ["Two weeks"])
        ]);
        return BrowserFormFillPacketBuilder.Create(job, form, FormFillAuthorizer.Authorize(job, form))!;
    }

    private static string Receipt(BrowserFormFillPacket packet, string email, string availability) =>
        JsonSerializer.Serialize(new
        {
            jobId = packet.JobId,
            url = packet.Url,
            authorizationFingerprint = packet.AuthorizationFingerprint,
            values = new[]
            {
                new { key = "email", label = "Email", value = email },
                new { key = "availability", label = "Availability", value = availability }
            }
        });
}
