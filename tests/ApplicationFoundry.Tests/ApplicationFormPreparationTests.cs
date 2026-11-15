using ApplicationFoundry.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApplicationFoundry.Tests;

public sealed class ApplicationFormPreparationTests
{
    [Fact]
    public async Task Migration_persists_per_opportunity_mode_rules_and_reviewed_answers()
    {
        var path = Path.Combine(Path.GetTempPath(), $"application-foundry-form-{Guid.NewGuid():N}.db");
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
                await db.Database.MigrateAsync();
                db.JobOpportunities.Add(new JobOpportunity
                {
                    UserId = "synthetic-applicant",
                    RoleTitle = "Systems engineer",
                    Description = "Synthetic employer form test"
                });
                await db.SaveChangesAsync();
            }

            await using (var db = await factory.CreateDbContextAsync())
            {
                var job = await db.JobOpportunities.SingleAsync();
                job.FormPlan = new ApplicationFormPlan
                {
                    Mode = FormSubmissionMode.ApproveEachSubmission,
                    Rules = "Only this synthetic employer",
                    Answers =
                    [
                        new ApplicationFormAnswer
                        {
                            FieldLabel = "Email",
                            Answer = "avery@example.test",
                            Source = "Saved profile"
                        }
                    ],
                    Entries =
                    [
                        new ApplicationFormEntry
                        {
                            FieldLabel = "Email",
                            ValueEntered = "avery@example.test"
                        }
                    ]
                };
                await db.SaveChangesAsync();
            }

            await using (var db = await factory.CreateDbContextAsync())
            {
                var job = await db.JobOpportunities.Include(item => item.FormPlan)
                    .ThenInclude(plan => plan!.Answers)
                    .Include(item => item.FormPlan).ThenInclude(plan => plan!.Entries).SingleAsync();
                Assert.Equal(FormSubmissionMode.ApproveEachSubmission, job.FormPlan!.Mode);
                Assert.Equal("Only this synthetic employer", job.FormPlan.Rules);
                var answer = Assert.Single(job.FormPlan.Answers);
                Assert.Equal("avery@example.test", answer.Answer);
                Assert.Equal("Saved profile", answer.Source);
                var entry = Assert.Single(job.FormPlan.Entries);
                Assert.Equal("avery@example.test", entry.ValueEntered);
                Assert.Null(entry.SubmittedAt);
                job.FormPlan.SubmissionApprovedAt = DateTimeOffset.UtcNow;
                job.FormPlan.SubmissionApprovalFingerprint = SubmissionApproval.Fingerprint(job);
                entry.SubmittedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }

            await using (var db = await factory.CreateDbContextAsync())
            {
                var entry = await db.ApplicationFormEntries.SingleAsync();
                Assert.NotNull(entry.SubmittedAt);
                var job = await db.JobOpportunities.Include(item => item.FormPlan)
                    .ThenInclude(plan => plan!.Entries).Include(item => item.Documents).SingleAsync();
                Assert.True(SubmissionApproval.IsCurrent(job));
                job.FormPlan!.Entries.Add(new ApplicationFormEntry
                {
                    FieldLabel = "Availability",
                    ValueEntered = "Two weeks"
                });
                Assert.False(SubmissionApproval.IsCurrent(job));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Approval_changes_when_an_approved_document_or_rules_change()
    {
        var job = new JobOpportunity
        {
            RoleTitle = "Synthetic analyst",
            Organization = "Example employer",
            FormPlan = new ApplicationFormPlan { Mode = FormSubmissionMode.ApproveEachSubmission, Rules = "One employer" },
            Documents =
            [
                new DocumentVersion { Id = 1, Kind = CareerDocumentKind.Resume, Version = 1, Content = "Synthetic resume", ApprovedAt = DateTimeOffset.UtcNow },
                new DocumentVersion { Id = 2, Kind = CareerDocumentKind.CoverLetter, Version = 1, Content = "Synthetic letter", ApprovedAt = DateTimeOffset.UtcNow }
            ]
        };
        job.FormPlan.SubmissionApprovedAt = DateTimeOffset.UtcNow;
        job.FormPlan.SubmissionApprovalFingerprint = SubmissionApproval.Fingerprint(job);
        Assert.True(SubmissionApproval.IsCurrent(job));
        job.Documents[0].Content = "Revised synthetic resume";
        Assert.False(SubmissionApproval.IsCurrent(job));
        job.Documents[0].Content = "Synthetic resume";
        job.FormPlan.Rules = "Different employer rule";
        Assert.False(SubmissionApproval.IsCurrent(job));
    }
}
