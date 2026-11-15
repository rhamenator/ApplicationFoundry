using System.Net;
using System.Text;
using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Documents;
using ApplicationFoundry.Features.Fit;
using ApplicationFoundry.Features.Forms;
using ApplicationFoundry.Features.Jobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApplicationFoundry.Tests;

public sealed class SyntheticWorkflowIntegrationTests
{
    [Fact]
    public async Task ImportedEvidenceFlowsThroughDiscoveryDraftExportAndFreshContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"application-foundry-workflow-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite($"Data Source={path};Pooling=False"));
        services.AddIdentityCore<ApplicationUser>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var importer = new CandidateDocumentImportService();
        var documents = new CareerDocumentService();
        try
        {
            var source = await importer.ExtractAsync("synthetic-resume.txt", "resume",
                Encoding.UTF8.GetBytes("Avery Rivera\navery@example.test\n• Built reliable release pipelines"));
            var claim = Assert.Single(CandidateFactMapper.Suggest(source.ExtractedText).Claims);
            var profile = new CandidateProfile
            {
                UserId = "synthetic-applicant",
                FirstName = "Avery",
                LastName = "Rivera",
                Email = "avery@example.test",
                Evidence = [new EvidenceItem { Claim = claim }]
            };
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
                db.CandidateProfiles.Add(profile);
                db.CandidateSourceDocuments.Add(new CandidateSourceDocument
                {
                    UserId = profile.UserId,
                    FileName = source.FileName,
                    Kind = source.Kind,
                    MediaType = source.MediaType,
                    SourceSha256 = source.SourceSha256,
                    ExtractedText = source.ExtractedText
                });
                await db.SaveChangesAsync();
            }

            var discovery = new ArbeitnowJobDiscoveryService(new HttpClient(new SyntheticFeed()), new ManagedFitScorer());
            var found = Assert.Single(await discovery.FindMatchesAsync(profile, new JobSearchFilters("engineer")));
            var job = new JobOpportunity
            {
                UserId = profile.UserId,
                RoleTitle = found.RoleTitle,
                Organization = found.Organization,
                SourceUrl = found.SourceUrl,
                Description = found.Description,
                FitScore = found.FitScore,
                FitExplanation = found.FitExplanation,
                NextFollowUpOn = new DateOnly(2026, 12, 1),
                FormPlan = new ApplicationFormPlan
                {
                    Mode = FormSubmissionMode.SubmitPersonally,
                    Answers = [new ApplicationFormAnswer { FieldLabel = "Email", Answer = profile.Email, Source = "Reviewed synthetic profile" }]
                }
            };
            job.Documents.Add(new DocumentVersion { Kind = CareerDocumentKind.Resume, Version = 1, Content = documents.DraftResume(profile, job) });
            job.Documents.Add(new DocumentVersion { Kind = CareerDocumentKind.CoverLetter, Version = 1, Content = documents.DraftCoverLetter(profile, job) });
            await using (var db = await factory.CreateDbContextAsync())
            {
                db.JobOpportunities.Add(job);
                await db.SaveChangesAsync();
            }

            await using (var db = await factory.CreateDbContextAsync())
            {
                var saved = await db.JobOpportunities.Include(item => item.Documents)
                    .Include(item => item.FormPlan).ThenInclude(plan => plan!.Answers).SingleAsync();
                Assert.Contains(claim, saved.Documents.Single(item => item.Kind == CareerDocumentKind.Resume).Content);
                var proposal = EmployerFormFillPlanner.Prepare(saved, new EmployerFormSnapshot(
                    new Uri("https://www.arbeitnow.com/apply"), [new EmployerFormField("email", "Email", "email", true)]));
                Assert.True(proposal.IsComplete);
                foreach (var version in saved.Documents)
                {
                    version.ApprovedAt = DateTimeOffset.UtcNow;
                    version.ApprovedByUserId = profile.UserId;
                }
                saved.Activities.Add(new ApplicationActivity { Action = "follow-up", Note = "Synthetic status inquiry planned." });
                await db.SaveChangesAsync();
            }

            await using (var db = await factory.CreateDbContextAsync())
            {
                var saved = await db.JobOpportunities.Include(item => item.Documents)
                    .Include(item => item.Activities).Include(item => item.FormPlan)
                    .ThenInclude(plan => plan!.Answers).SingleAsync();
                Assert.All(saved.Documents, version => Assert.NotNull(version.ApprovedAt));
                Assert.Equal(new DateOnly(2026, 12, 1), saved.NextFollowUpOn);
                Assert.Single(saved.Activities);
                Assert.Equal("avery@example.test", Assert.Single(saved.FormPlan!.Answers).Answer);
                var resume = saved.Documents.Single(item => item.Kind == CareerDocumentKind.Resume);
                var letter = saved.Documents.Single(item => item.Kind == CareerDocumentKind.CoverLetter);
                Assert.Contains(claim, (await importer.ExtractAsync("approved-resume.docx", "resume", documents.ToDocx(resume.Content))).ExtractedText);
                Assert.Contains("Avery", (await importer.ExtractAsync("approved-letter.pdf", "cover-letter", documents.ToPdf(letter.Content))).ExtractedText);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class SyntheticFeed : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const string feed = """
                {"data":[{"title":"Release Engineer","company_name":"Example Employer","location":"Remote","description":"<p>Build reliable release pipelines</p>","url":"https://www.arbeitnow.com/jobs/companies/example/release-engineer"}]}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(feed) });
        }
    }
}
