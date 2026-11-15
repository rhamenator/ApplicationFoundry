using ApplicationFoundry.Data;
using Microsoft.EntityFrameworkCore;

namespace ApplicationFoundry.Features.Documents;

public sealed class LegacyEvidenceImportException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class LegacyEvidenceImportService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<LegacyEvidenceImportBatch> ImportAsync(
        string userId, LegacyEvidenceImportPlan plan, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || plan.Claims.Count is < 1 or > 5000
            || plan.FileName.Length is < 1 or > 200
            || Path.GetFileName(plan.FileName.Replace('\\', '/')) != plan.FileName
            || plan.SourceSha256.Length != 64 || !plan.SourceSha256.All(Uri.IsHexDigit)
            || plan.Claims.Any(claim => string.IsNullOrWhiteSpace(claim) || claim.Length > 240
                || claim != claim.Trim())
            || plan.Claims.Distinct(StringComparer.OrdinalIgnoreCase).Count() != plan.Claims.Count)
            throw new LegacyEvidenceImportException("PLAN_INVALID");

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var profile = await db.CandidateProfiles.SingleOrDefaultAsync(
            item => item.UserId == userId, cancellationToken);
        if (profile is null) throw new LegacyEvidenceImportException("PROFILE_REQUIRED");

        var existing = await db.EvidenceItems.Where(item => item.ProfileId == profile.Id)
            .Select(item => item.Claim).ToListAsync(cancellationToken);
        var existingClaims = existing.Select(claim => claim.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (plan.Claims.Any(existingClaims.Contains))
            throw new LegacyEvidenceImportException("CLAIM_EXISTS");

        var batch = new LegacyEvidenceImportBatch
        {
            UserId = userId,
            ProfileId = profile.Id,
            FileName = plan.FileName,
            SourceSha256 = plan.SourceSha256.ToLowerInvariant(),
            ClaimCount = plan.Claims.Count
        };
        db.LegacyEvidenceImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var claim in plan.Claims)
            db.EvidenceItems.Add(new EvidenceItem
            {
                ProfileId = profile.Id,
                LegacyImportBatchId = batch.Id,
                Kind = "achievement",
                Claim = claim
            });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return batch;
    }

    public async Task<int> UndoAsync(string userId, int batchId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || batchId <= 0)
            throw new LegacyEvidenceImportException("BATCH_NOT_FOUND");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await db.LegacyEvidenceImportBatches.SingleOrDefaultAsync(
            item => item.Id == batchId && item.UserId == userId, cancellationToken);
        if (batch is null) throw new LegacyEvidenceImportException("BATCH_NOT_FOUND");
        var items = await db.EvidenceItems.Where(item => item.LegacyImportBatchId == batch.Id
            && item.ProfileId == batch.ProfileId).ToListAsync(cancellationToken);
        db.EvidenceItems.RemoveRange(items);
        db.LegacyEvidenceImportBatches.Remove(batch);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return items.Count;
    }
}
