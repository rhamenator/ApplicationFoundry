using ApplicationFoundry.Data;
using Microsoft.EntityFrameworkCore;

namespace ApplicationFoundry.Features.Forms;

public sealed record BrowserFormEntryRecordResult(bool Recorded, int Count, string Reason);

public static class BrowserFormEntryRecorder
{
    public static async Task<BrowserFormEntryRecordResult> RecordAsync(
        ApplicationDbContext db, int jobId, string userId, EmployerFormSnapshot snapshot,
        FormFillAuthorization? authorization, string? receiptJson, bool applicantConfirmed,
        CancellationToken cancellationToken = default)
    {
        if (!applicantConfirmed)
            return new(false, 0, "Inspect the employer form and confirm its exact values before recording them.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var job = await db.JobOpportunities.AsSplitQuery()
            .Include(item => item.FormPlan).ThenInclude(plan => plan!.Answers)
            .Include(item => item.FormPlan).ThenInclude(plan => plan!.Entries)
            .SingleOrDefaultAsync(item => item.Id == jobId && item.UserId == userId, cancellationToken);
        var packet = job is null ? null : BrowserFormFillPacketBuilder.Create(job, snapshot, authorization);
        if (packet is null || !BrowserFormFillReceiptValidator.TryMatch(receiptJson, packet, out var entered, out _))
            return new(false, 0, "The browser receipt or authorization changed. Preview and authorize the form again.");
        if (entered.Any(value => job!.FormPlan!.Entries.Any(entry =>
                string.Equals(entry.FieldLabel, value.Label, StringComparison.OrdinalIgnoreCase))))
            return new(false, 0, "A field in this receipt is already recorded. Remove its unsent entry before importing again.");

        var recordedAt = DateTimeOffset.UtcNow;
        foreach (var value in entered)
        {
            job!.FormPlan!.Entries.Add(new ApplicationFormEntry
            {
                FieldLabel = value.Label,
                ValueEntered = value.Value,
                EnteredAt = recordedAt
            });
        }
        db.Set<ApplicationActivity>().Add(new ApplicationActivity
        {
            JobOpportunityId = jobId,
            Action = "form-filled",
            Note = $"Applicant reviewed a browser fill receipt for {entered.Count} field(s). Values recorded as entered, not submitted.",
            OccurredAt = recordedAt
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, entered.Count, "Browser-filled values recorded locally as entered, not submitted.");
    }
}
