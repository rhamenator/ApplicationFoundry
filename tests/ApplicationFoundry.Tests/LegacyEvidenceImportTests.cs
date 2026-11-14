using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Documents;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApplicationFoundry.Tests;

public sealed class LegacyEvidenceImportTests
{
    [Fact]
    public async Task ImportsReviewedClaimsAndUndoesOnlyItsOwnBatch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"application-foundry-legacy-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite($"Data Source={path}"));
        services.AddIdentityCore<ApplicationUser>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        try
        {
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.MigrateAsync("20261112133759_SubmissionApproval");
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO CandidateProfiles
                    (Id, UserId, Title, FirstName, MiddleInitials, LastName, Suffix,
                     PreferredDisplayName, Email, Phone, Summary)
                    VALUES (1, 'synthetic-applicant', '', 'Jordan', '', 'Example', '', '', '', '', '')
                    """);
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO EvidenceItems (ProfileId, Kind, Claim, Details, Keywords)
                    VALUES (1, 'achievement', 'Existing synthetic evidence', '', '')
                    """);
                await db.Database.MigrateAsync();
            }

            var previewer = new LegacyAccessCsvPreviewService();
            var preview = previewer.Preview("evidence.csv", "Claim\nSynthetic claim one\nSynthetic claim two"u8.ToArray());
            var plan = previewer.PrepareEvidenceImport(preview, ["evidence-claim"]);
            var importer = new LegacyEvidenceImportService(factory);
            var batch = await importer.ImportAsync("synthetic-applicant", plan);

            await using (var db = await factory.CreateDbContextAsync())
            {
                Assert.Equal(3, await db.EvidenceItems.CountAsync());
                Assert.Equal(2, await db.EvidenceItems.CountAsync(item => item.LegacyImportBatchId == batch.Id));
                var saved = await db.LegacyEvidenceImportBatches.SingleAsync();
                Assert.Equal(preview.SourceSha256, saved.SourceSha256);
                Assert.Equal("evidence.csv", saved.FileName);
            }
            var wrongUser = await Assert.ThrowsAsync<LegacyEvidenceImportException>(
                () => importer.UndoAsync("other-applicant", batch.Id));
            Assert.Equal("BATCH_NOT_FOUND", wrongUser.Code);
            var duplicate = await Assert.ThrowsAsync<LegacyEvidenceImportException>(
                () => importer.ImportAsync("synthetic-applicant", plan));
            Assert.Equal("CLAIM_EXISTS", duplicate.Code);
            Assert.Equal(2, await importer.UndoAsync("synthetic-applicant", batch.Id));

            await using (var db = await factory.CreateDbContextAsync())
            {
                Assert.Equal("Existing synthetic evidence", (await db.EvidenceItems.SingleAsync()).Claim);
                Assert.Empty(await db.LegacyEvidenceImportBatches.ToListAsync());
            }
        }
        finally { File.Delete(path); }
    }
}
