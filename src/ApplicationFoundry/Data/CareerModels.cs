using System.ComponentModel.DataAnnotations;

namespace ApplicationFoundry.Data;

public sealed class CandidateProfile
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = "";
    [MaxLength(32)] public string Title { get; set; } = "";
    [Required, MaxLength(80)] public string FirstName { get; set; } = "";
    [MaxLength(40)] public string MiddleInitials { get; set; } = "";
    [Required, MaxLength(80)] public string LastName { get; set; } = "";
    [MaxLength(32)] public string Suffix { get; set; } = "";
    [MaxLength(160)] public string PreferredDisplayName { get; set; } = "";
    [EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
    [Phone, MaxLength(64)] public string Phone { get; set; } = "";
    [MaxLength(4000)] public string Summary { get; set; } = "";
    public List<EvidenceItem> Evidence { get; set; } = [];

    public string DisplayName => !string.IsNullOrWhiteSpace(PreferredDisplayName)
        ? PreferredDisplayName
        : string.Join(' ', new[] { Title, FirstName, MiddleInitials, LastName, Suffix }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed class EvidenceItem
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    [Required, MaxLength(80)] public string Kind { get; set; } = "achievement";
    [Required, MaxLength(240)] public string Claim { get; set; } = "";
    [MaxLength(4000)] public string Details { get; set; } = "";
    [MaxLength(1000)] public string Keywords { get; set; } = "";
}

public enum OpportunityStatus
{
    Interested,
    Drafting,
    ReadyForReview,
    Approved,
    Submitted,
    Interviewing,
    Closed
}

public sealed class JobOpportunity
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = "";
    [Required, MaxLength(200)] public string RoleTitle { get; set; } = "";
    [MaxLength(200)] public string Organization { get; set; } = "";
    [MaxLength(2048)] public string SourceUrl { get; set; } = "";
    [Required, MaxLength(30000)] public string Description { get; set; } = "";
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Interested;
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
    public float FitScore { get; set; }
    [MaxLength(2000)] public string FitExplanation { get; set; } = "";
    public List<DocumentVersion> Documents { get; set; } = [];
    public List<ApplicationActivity> Activities { get; set; } = [];
}

public enum CareerDocumentKind { Resume, CoverLetter }

public sealed class DocumentVersion
{
    public int Id { get; set; }
    public int JobOpportunityId { get; set; }
    public CareerDocumentKind Kind { get; set; }
    public int Version { get; set; }
    [Required, MaxLength(40000)] public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ApprovedAt { get; set; }
    [MaxLength(450)] public string ApprovedByUserId { get; set; } = "";
}

public sealed class ApplicationActivity
{
    public int Id { get; set; }
    public int JobOpportunityId { get; set; }
    [Required, MaxLength(80)] public string Action { get; set; } = "";
    [MaxLength(1000)] public string Note { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
