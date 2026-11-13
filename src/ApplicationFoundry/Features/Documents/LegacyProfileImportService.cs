using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ApplicationFoundry.Data;
using Microsoft.EntityFrameworkCore;

namespace ApplicationFoundry.Features.Documents;

public sealed class LegacyProfileImportException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class LegacyProfileImportService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<LegacyProfileImportBatch> ImportAsync(
        string userId, LegacyProfileImportPlan plan, CancellationToken cancellationToken = default)
    {
        ValidatePlan(userId, plan);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var profile = await db.CandidateProfiles.SingleOrDefaultAsync(
            item => item.UserId == userId, cancellationToken);
        if (profile is null) throw new LegacyProfileImportException("PROFILE_REQUIRED");

        var before = plan.Fields.Keys.ToDictionary(key => key, key => GetField(profile, key));
        foreach (var (key, value) in plan.Fields) SetField(profile, key, value);
        if (!Validator.TryValidateObject(profile, new ValidationContext(profile), [], true))
            throw new LegacyProfileImportException("PROFILE_INVALID");

        var batch = new LegacyProfileImportBatch
        {
            UserId = userId,
            ProfileId = profile.Id,
            FileName = plan.FileName,
            SourceSha256 = plan.SourceSha256.ToLowerInvariant(),
            BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(plan.Fields)
        };
        db.LegacyProfileImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return batch;
    }

    public async Task<int> UndoAsync(string userId, int batchId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || batchId <= 0)
            throw new LegacyProfileImportException("BATCH_NOT_FOUND");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await db.LegacyProfileImportBatches.SingleOrDefaultAsync(
            item => item.Id == batchId && item.UserId == userId, cancellationToken);
        if (batch is null) throw new LegacyProfileImportException("BATCH_NOT_FOUND");
        var profile = await db.CandidateProfiles.SingleOrDefaultAsync(
            item => item.Id == batch.ProfileId && item.UserId == userId, cancellationToken);
        if (profile is null) throw new LegacyProfileImportException("BATCH_NOT_FOUND");

        var before = ReadFields(batch.BeforeJson);
        var after = ReadFields(batch.AfterJson);
        if (before.Count == 0 || before.Count != after.Count || before.Keys.Except(after.Keys).Any())
            throw new LegacyProfileImportException("BATCH_INVALID");
        if (after.Any(field => GetField(profile, field.Key) != field.Value))
            throw new LegacyProfileImportException("PROFILE_CHANGED");
        foreach (var (key, value) in before) SetField(profile, key, value);
        db.LegacyProfileImportBatches.Remove(batch);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return before.Count;
    }

    private static Dictionary<string, string> ReadFields(string json)
    {
        try
        {
            var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? throw new JsonException();
            if (fields.Keys.Any(key => !AllowedFields.Contains(key))) throw new JsonException();
            return fields;
        }
        catch (JsonException) { throw new LegacyProfileImportException("BATCH_INVALID"); }
    }

    private static readonly HashSet<string> AllowedFields =
        ["title", "first-name", "middle-initials", "last-name", "suffix",
            "preferred-display-name", "email", "phone", "summary"];

    private static void ValidatePlan(string userId, LegacyProfileImportPlan plan)
    {
        if (string.IsNullOrWhiteSpace(userId) || plan.Fields.Count is < 1 or > 9
            || plan.FileName.Length is < 1 or > 200
            || Path.GetFileName(plan.FileName.Replace('\\', '/')) != plan.FileName
            || plan.SourceSha256.Length != 64 || !plan.SourceSha256.All(Uri.IsHexDigit))
            throw new LegacyProfileImportException("PLAN_INVALID");
        foreach (var (key, value) in plan.Fields)
        {
            var limit = key switch
            {
                "title" or "suffix" => 32,
                "first-name" or "last-name" => 80,
                "middle-initials" => 40,
                "preferred-display-name" => 160,
                "email" => 256,
                "phone" => 64,
                "summary" => 4000,
                _ => throw new LegacyProfileImportException("PLAN_INVALID")
            };
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > limit
                || (key == "email" && !new EmailAddressAttribute().IsValid(value)))
                throw new LegacyProfileImportException("PLAN_INVALID");
        }
    }

    private static string GetField(CandidateProfile profile, string key) => key switch
    {
        "title" => profile.Title,
        "first-name" => profile.FirstName,
        "middle-initials" => profile.MiddleInitials,
        "last-name" => profile.LastName,
        "suffix" => profile.Suffix,
        "preferred-display-name" => profile.PreferredDisplayName,
        "email" => profile.Email,
        "phone" => profile.Phone,
        "summary" => profile.Summary,
        _ => throw new LegacyProfileImportException("BATCH_INVALID")
    };

    private static void SetField(CandidateProfile profile, string key, string value)
    {
        switch (key)
        {
            case "title": profile.Title = value; break;
            case "first-name": profile.FirstName = value; break;
            case "middle-initials": profile.MiddleInitials = value; break;
            case "last-name": profile.LastName = value; break;
            case "suffix": profile.Suffix = value; break;
            case "preferred-display-name": profile.PreferredDisplayName = value; break;
            case "email": profile.Email = value; break;
            case "phone": profile.Phone = value; break;
            case "summary": profile.Summary = value; break;
            default: throw new LegacyProfileImportException("BATCH_INVALID");
        }
    }
}
