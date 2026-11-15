using System.Text.Json;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApplicationFoundry.Tests;

public sealed class BrowserFormEntryRecorderTests
{
    [Fact]
    public async Task Applicant_confirmed_exact_receipt_records_entries_once_without_submission()
    {
        var path = Path.Combine(Path.GetTempPath(), $"application-foundry-browser-receipt-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite($"Data Source={path}"));
        services.AddIdentityCore<ApplicationUser>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        try
        {
            int jobId;
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
                var job = new JobOpportunity
                {
                    UserId = "synthetic-applicant",
                    RoleTitle = "Synthetic engineer",
                    SourceUrl = "https://example.test/job",
                    FormPlan = new ApplicationFormPlan
                    {
                        Answers =
                        [
                            new ApplicationFormAnswer
                            {
                                FieldLabel = "Email", Answer = "avery@example.test", Source = "Reviewed profile"
                            },
                            new ApplicationFormAnswer
                            {
                                FieldLabel = "Availability", Answer = "Two weeks", Source = "Reviewed applicant statement"
                            }
                        ]
                    }
                };
                db.JobOpportunities.Add(job);
                await db.SaveChangesAsync();
                jobId = job.Id;
            }

            var form = new EmployerFormSnapshot(new Uri("https://example.test/apply"),
            [
                new EmployerFormField("email", "Email", "email", true),
                new EmployerFormField("availability", "Availability", "select", true, ["Two weeks"])
            ]);
            FormFillAuthorization authorization;
            string receipt;
            await using (var db = await factory.CreateDbContextAsync())
            {
                var job = await db.JobOpportunities.Include(item => item.FormPlan)
                    .ThenInclude(plan => plan!.Answers).SingleAsync();
                authorization = FormFillAuthorizer.Authorize(job, form)!;
                var packet = BrowserFormFillPacketBuilder.Create(job, form, authorization)!;
                receipt = JsonSerializer.Serialize(new
                {
                    jobId = packet.JobId,
                    url = packet.Url,
                    authorizationFingerprint = packet.AuthorizationFingerprint,
                    values = new[]
                    {
                        new { key = "email", label = "Email", value = "avery@example.test" },
                        new { key = "availability", label = "Availability", value = "Two weeks" }
                    }
                });
            }

            await using (var db = await factory.CreateDbContextAsync())
            {
                var unconfirmed = await BrowserFormEntryRecorder.RecordAsync(db, jobId, "synthetic-applicant",
                    form, authorization, receipt, false);
                Assert.False(unconfirmed.Recorded);
                var recorded = await BrowserFormEntryRecorder.RecordAsync(db, jobId, "synthetic-applicant",
                    form, authorization, receipt, true);
                Assert.True(recorded.Recorded, recorded.Reason);
                Assert.Equal(2, recorded.Count);
            }
            await using (var db = await factory.CreateDbContextAsync())
            {
                var job = await db.JobOpportunities.Include(item => item.FormPlan)
                    .ThenInclude(plan => plan!.Entries).Include(item => item.Activities).SingleAsync();
                Assert.Equal(OpportunityStatus.Interested, job.Status);
                Assert.Equal(2, job.FormPlan!.Entries.Count);
                Assert.Contains(job.FormPlan.Entries, entry => entry.FieldLabel == "Email" && entry.ValueEntered == "avery@example.test");
                Assert.Contains(job.FormPlan.Entries, entry => entry.FieldLabel == "Availability" && entry.ValueEntered == "Two weeks");
                Assert.All(job.FormPlan.Entries, entry => Assert.Null(entry.SubmittedAt));
                Assert.Equal("form-filled", Assert.Single(job.Activities).Action);
                var replay = await BrowserFormEntryRecorder.RecordAsync(db, jobId, "synthetic-applicant",
                    form, authorization, receipt, true);
                Assert.False(replay.Recorded);
                Assert.Equal(2, job.FormPlan.Entries.Count);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
