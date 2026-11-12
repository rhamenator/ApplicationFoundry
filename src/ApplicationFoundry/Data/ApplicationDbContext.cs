using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ApplicationFoundry.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();
    public DbSet<CandidateSourceDocument> CandidateSourceDocuments => Set<CandidateSourceDocument>();
    public DbSet<JobOpportunity> JobOpportunities => Set<JobOpportunity>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<ApplicationActivity> ApplicationActivities => Set<ApplicationActivity>();
    public DbSet<ApplicationFormPlan> ApplicationFormPlans => Set<ApplicationFormPlan>();
    public DbSet<ApplicationFormAnswer> ApplicationFormAnswers => Set<ApplicationFormAnswer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<CandidateProfile>().HasIndex(profile => profile.UserId).IsUnique();
        builder.Entity<CandidateSourceDocument>().HasIndex(document => new { document.UserId, document.ImportedAt });
        builder.Entity<JobOpportunity>().HasIndex(job => new { job.UserId, job.Status });
        builder.Entity<EvidenceItem>()
            .HasOne<CandidateProfile>()
            .WithMany(profile => profile.Evidence)
            .HasForeignKey(item => item.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<DocumentVersion>()
            .HasOne<JobOpportunity>()
            .WithMany(job => job.Documents)
            .HasForeignKey(document => document.JobOpportunityId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ApplicationActivity>()
            .HasOne<JobOpportunity>()
            .WithMany(job => job.Activities)
            .HasForeignKey(activity => activity.JobOpportunityId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ApplicationFormPlan>()
            .HasOne<JobOpportunity>()
            .WithOne(job => job.FormPlan)
            .HasForeignKey<ApplicationFormPlan>(plan => plan.JobOpportunityId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ApplicationFormAnswer>()
            .HasOne<ApplicationFormPlan>()
            .WithMany(plan => plan.Answers)
            .HasForeignKey(answer => answer.ApplicationFormPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
