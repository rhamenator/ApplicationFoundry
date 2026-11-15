using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Documents;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApplicationFoundry.Tests;

public sealed class LegacyProfileImportTests
{
    [Fact]
    public async Task UpgradesExistingProfileAndUndoesOnlyUnchangedImportedFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"application-foundry-profile-import-{Guid.NewGuid():N}.db");
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
                await db.Database.MigrateAsync("20261113073800_LegacyEvidenceImportBatch");
                db.CandidateProfiles.Add(new CandidateProfile
                {
                    UserId = "synthetic-applicant",
                    Title = "Dr.",
                    FirstName = "Jordan",
                    MiddleInitials = "Q.",
                    LastName = "Example",
                    Suffix = "Jr.",
                    PreferredDisplayName = "J. Example",
                    Email = "old@example.test",
                    Summary = "Existing synthetic summary"
                });
                await db.SaveChangesAsync();
                await db.Database.MigrateAsync();
            }

            var previewer = new LegacyAccessCsvPreviewService();
            var preview = previewer.Preview("profile.csv",
                "Title,Given,Middle,Family,Suffix,Display,Email\nMx.,Avery,R.,Rivera,III,Avery Rivera,new@example.test"u8.ToArray());
            var plan = previewer.PrepareProfileImport(preview,
                ["title", "first-name", "middle-initials", "last-name", "suffix", "preferred-display-name", "email"]);
            var importer = new LegacyProfileImportService(factory);
            var batch = await importer.ImportAsync("synthetic-applicant", plan);

            await using (var db = await factory.CreateDbContextAsync())
            {
                var profile = await db.CandidateProfiles.SingleAsync();
                Assert.Equal("Avery", profile.FirstName);
                Assert.Equal("Mx.", profile.Title);
                Assert.Equal("R.", profile.MiddleInitials);
                Assert.Equal("Rivera", profile.LastName);
                Assert.Equal("III", profile.Suffix);
                Assert.Equal("Avery Rivera", profile.PreferredDisplayName);
                Assert.Equal("new@example.test", profile.Email);
                Assert.Equal("Existing synthetic summary", profile.Summary);
                Assert.Equal(preview.SourceSha256,
                    (await db.LegacyProfileImportBatches.SingleAsync()).SourceSha256);
                profile.FirstName = "Manually edited";
                profile.Summary = "Updated unrelated summary";
                await db.SaveChangesAsync();
            }

            Assert.Equal("BATCH_NOT_FOUND", (await Assert.ThrowsAsync<LegacyProfileImportException>(
                () => importer.UndoAsync("other-applicant", batch.Id))).Code);
            Assert.Equal("PROFILE_CHANGED", (await Assert.ThrowsAsync<LegacyProfileImportException>(
                () => importer.UndoAsync("synthetic-applicant", batch.Id))).Code);

            await using (var db = await factory.CreateDbContextAsync())
            {
                var profile = await db.CandidateProfiles.SingleAsync();
                Assert.Equal("Manually edited", profile.FirstName);
                Assert.Equal("new@example.test", profile.Email);
                profile.FirstName = "Avery";
                await db.SaveChangesAsync();
            }
            Assert.Equal(7, await importer.UndoAsync("synthetic-applicant", batch.Id));

            await using (var db = await factory.CreateDbContextAsync())
            {
                var profile = await db.CandidateProfiles.SingleAsync();
                Assert.Equal("Jordan", profile.FirstName);
                Assert.Equal("Dr.", profile.Title);
                Assert.Equal("Q.", profile.MiddleInitials);
                Assert.Equal("Example", profile.LastName);
                Assert.Equal("Jr.", profile.Suffix);
                Assert.Equal("J. Example", profile.PreferredDisplayName);
                Assert.Equal("old@example.test", profile.Email);
                Assert.Equal("Updated unrelated summary", profile.Summary);
                Assert.Empty(await db.LegacyProfileImportBatches.ToListAsync());
            }
        }
        finally { File.Delete(path); }
    }
}
