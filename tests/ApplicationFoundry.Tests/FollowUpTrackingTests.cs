using ApplicationFoundry.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApplicationFoundry.Tests;

public sealed class FollowUpTrackingTests
{
    [Fact]
    public async Task MigrationPersistsScheduledDateAndCompletedFollowUp()
    {
        var path = Path.Combine(Path.GetTempPath(), $"application-foundry-follow-up-{Guid.NewGuid():N}.db");
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
                    Description = "Synthetic follow-up test",
                    NextFollowUpOn = new DateOnly(2026, 12, 1)
                });
                await db.SaveChangesAsync();
            }

            await using (var db = await factory.CreateDbContextAsync())
            {
                var job = await db.JobOpportunities.SingleAsync();
                Assert.Equal(new DateOnly(2026, 12, 1), job.NextFollowUpOn);
                job.Activities.Add(new ApplicationActivity { Action = "follow-up", Note = "Sent a synthetic status inquiry." });
                job.NextFollowUpOn = null;
                await db.SaveChangesAsync();
            }

            await using (var db = await factory.CreateDbContextAsync())
            {
                var job = await db.JobOpportunities.Include(item => item.Activities).SingleAsync();
                Assert.Null(job.NextFollowUpOn);
                var activity = Assert.Single(job.Activities);
                Assert.Equal("follow-up", activity.Action);
                Assert.Equal("Sent a synthetic status inquiry.", activity.Note);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
