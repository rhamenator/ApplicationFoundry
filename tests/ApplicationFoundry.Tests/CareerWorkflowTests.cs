using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Documents;
using ApplicationFoundry.Features.Fit;
using System.IO.Compression;

namespace ApplicationFoundry.Tests;

public sealed class CareerWorkflowTests
{
    private static CandidateProfile Profile() => new()
    {
        Title = "Dr.",
        FirstName = "Avery",
        MiddleInitials = "J.",
        LastName = "Rivera",
        Suffix = "PhD",
        PreferredDisplayName = "Avery Rivera",
        Email = "avery@example.test",
        Phone = "555-0100",
        Summary = "Systems engineer building reliable distributed software.",
        Evidence =
        [
            new EvidenceItem { Claim = "Reduced service latency", Details = "Measured a 35 percent reduction.", Keywords = "rust,distributed,reliability" },
            new EvidenceItem { Claim = "Led incident reviews", Details = "Created durable corrective actions.", Keywords = "operations,reliability" }
        ]
    };

    [Fact]
    public void Standard_name_fields_use_preferred_display_name()
    {
        var profile = Profile();
        Assert.Equal("Avery Rivera", profile.DisplayName);
        profile.PreferredDisplayName = "";
        Assert.Equal("Dr. Avery J. Rivera PhD", profile.DisplayName);
    }

    [Fact]
    public void Local_onnx_model_returns_explainable_probability()
    {
        using var scorer = new OnnxFitScorer();
        var result = scorer.Score(Profile(), new JobOpportunity
        {
            RoleTitle = "Distributed systems engineer",
            Description = "Build reliable distributed services using Rust and lead operations reviews."
        });
        Assert.InRange(result.Score, 0.0f, 1.0f);
        Assert.Equal(4, result.Features.Length);
        Assert.Contains("Strongest signal", result.Explanation);
    }

    [Fact]
    public void Drafts_are_grounded_in_recorded_evidence()
    {
        var service = new CareerDocumentService();
        var resume = service.DraftResume(Profile(), new JobOpportunity { RoleTitle = "Engineer", Description = "Rust reliability" });
        Assert.Contains("Reduced service latency", resume);
        Assert.DoesNotContain("guaranteed", resume, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Docx_is_a_valid_open_xml_package()
    {
        var bytes = new CareerDocumentService().ToDocx("Avery Rivera\nEvidence-based draft");
        using var archive = new ZipArchive(new MemoryStream(bytes));
        Assert.NotNull(archive.GetEntry("word/document.xml"));
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
    }

    [Fact]
    public void Pdf_has_a_valid_header_and_cross_reference()
    {
        var bytes = new CareerDocumentService().ToPdf("Avery Rivera\nEvidence-based draft");
        var text = System.Text.Encoding.ASCII.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("xref", text);
        Assert.EndsWith("%%EOF\n", text);
    }
}
