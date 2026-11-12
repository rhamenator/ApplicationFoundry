using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApplicationFoundry.Data;

namespace ApplicationFoundry.Features.Forms;

public sealed record FormFillAuthorization(string Fingerprint, DateTimeOffset AuthorizedAt);

public static class FormFillAuthorizer
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    public static FormFillAuthorization? Authorize(JobOpportunity job, EmployerFormSnapshot form)
    {
        var proposal = EmployerFormFillPlanner.Prepare(job, form);
        return proposal.IsComplete
            ? new FormFillAuthorization(Fingerprint(job, form, proposal), DateTimeOffset.UtcNow)
            : null;
    }

    public static bool IsCurrent(JobOpportunity job, EmployerFormSnapshot form, FormFillAuthorization? authorization)
    {
        if (authorization is null)
            return false;
        var age = DateTimeOffset.UtcNow - authorization.AuthorizedAt;
        if (age < TimeSpan.Zero || age > MaxAge)
            return false;
        var proposal = EmployerFormFillPlanner.Prepare(job, form);
        if (!proposal.IsComplete)
            return false;
        try
        {
            var actual = Convert.FromHexString(Fingerprint(job, form, proposal));
            return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(authorization.Fingerprint));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Fingerprint(JobOpportunity job, EmployerFormSnapshot form, FormFillProposal proposal)
    {
        var packet = JsonSerializer.Serialize(new
        {
            job.Id,
            job.Organization,
            job.SourceUrl,
            job.Status,
            Mode = job.FormPlan!.Mode,
            job.FormPlan.Rules,
            FormUrl = form.Url.AbsoluteUri,
            form.Fields,
            proposal.Values
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packet)));
    }
}
