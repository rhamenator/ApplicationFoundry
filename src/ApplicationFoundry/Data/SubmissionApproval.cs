using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApplicationFoundry.Data;

public static class SubmissionApproval
{
    public static string Fingerprint(JobOpportunity job)
    {
        var plan = job.FormPlan ?? throw new InvalidOperationException("A form plan is required.");
        var snapshot = new
        {
            job.RoleTitle,
            job.Organization,
            job.SourceUrl,
            plan.Mode,
            plan.Rules,
            plan.UpdatedAt,
            Documents = job.Documents.OrderBy(document => document.Id).Select(document => new
            {
                document.Id,
                document.Kind,
                document.Version,
                document.Content,
                document.ApprovedAt
            }),
            Entries = plan.Entries.OrderBy(entry => entry.Id).Select(entry => new
            {
                entry.Id,
                entry.FieldLabel,
                entry.ValueEntered
            })
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot))));
    }

    public static bool IsCurrent(JobOpportunity job) =>
        job.FormPlan?.SubmissionApprovedAt is not null &&
        job.FormPlan.SubmissionApprovalFingerprint == Fingerprint(job);
}
