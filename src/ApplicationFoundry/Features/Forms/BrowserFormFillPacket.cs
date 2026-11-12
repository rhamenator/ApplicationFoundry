using System.Text.Json;
using System.Text.Json.Serialization;
using ApplicationFoundry.Data;

namespace ApplicationFoundry.Features.Forms;

public sealed record BrowserFormFillPacket(
    string Url,
    IReadOnlyList<EmployerFormField> Fields,
    IReadOnlyList<ProposedFieldValue> Values,
    DateTimeOffset ExpiresAt);

public static class BrowserFormFillPacketBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static BrowserFormFillPacket? Create(JobOpportunity job, EmployerFormSnapshot form, FormFillAuthorization? authorization)
    {
        if (!FormFillAuthorizer.IsCurrent(job, form, authorization))
            return null;
        var proposal = EmployerFormFillPlanner.Prepare(job, form);
        return proposal.IsComplete
            ? new BrowserFormFillPacket(form.Url.AbsoluteUri, form.Fields, proposal.Values,
                authorization!.AuthorizedAt.Add(FormFillAuthorizer.MaxAge))
            : null;
    }

    public static string ToJson(BrowserFormFillPacket packet) => JsonSerializer.Serialize(packet, JsonOptions);
}
