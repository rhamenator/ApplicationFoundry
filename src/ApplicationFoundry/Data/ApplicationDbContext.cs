using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ApplicationFoundry.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();
    public DbSet<JobOpportunity> JobOpportunities => Set<JobOpportunity>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<ApplicationActivity> ApplicationActivities => Set<ApplicationActivity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<CandidateProfile>().HasIndex(profile => profile.UserId).IsUnique();
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
    }
}
