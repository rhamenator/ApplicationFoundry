using ApplicationFoundry.Data;
using ApplicationFoundry.Features.Documents;
using ApplicationFoundry.Features.Fit;
using System.IO.Compression;
using System.ComponentModel.DataAnnotations;
using UglyToad.PdfPig;

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
    public void Profile_allows_blank_optional_contact_fields()
    {
        var profile = new CandidateProfile { UserId = "synthetic", FirstName = "Jordan", LastName = "Example" };
        var results = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(profile, new ValidationContext(profile), results, true));
        Assert.Empty(results);
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
    public void Managed_desktop_matcher_agrees_with_onnx_model()
    {
        var job = new JobOpportunity
        {
            RoleTitle = "Distributed systems engineer",
            Description = "Build reliable distributed services using Rust and lead operations reviews."
        };
        using var onnx = new OnnxFitScorer();
        var native = onnx.Score(Profile(), job);
        var managed = new ManagedFitScorer().Score(Profile(), job);

        Assert.InRange(Math.Abs(native.Score - managed.Score), 0, 0.00001f);
        Assert.Equal(native.Features, managed.Features);
        Assert.Equal(native.Explanation, managed.Explanation);
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
    public void Drafts_avoid_empty_contact_separators_and_repeated_evidence()
    {
        var profile = Profile();
        profile.Phone = "";
        profile.Evidence =
        [
            new EvidenceItem
            {
                Claim = "Reviewed synthetic application workflows.",
                Details = "Reviewed synthetic application workflows and documented repeatable results.",
                Keywords = "review"
            }
        ];
        var job = new JobOpportunity { RoleTitle = "QA specialist", Description = "Review workflows" };
        var service = new CareerDocumentService();

        var resume = service.DraftResume(profile, job);
        var letter = service.DraftCoverLetter(profile, job);

        Assert.Contains("Avery Rivera\navery@example.test\n\nPROFILE", resume);
        Assert.Contains("• Reviewed synthetic application workflows and documented repeatable results.", resume);
        Assert.DoesNotContain("workflows.:", resume);
        Assert.Contains("Relevant evidence includes Reviewed synthetic application workflows.", letter);
        Assert.DoesNotContain("workflows..", letter);
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

    [Fact]
    public void Pdf_resume_name_is_larger_than_body_text()
    {
        var content = "Avery Rivera\navery@example.test\n\nPROFILE\nSynthetic QA specialist.\n\nSELECTED EVIDENCE\n• Reviewed a test workflow.";
        var bytes = new CareerDocumentService().ToPdf(content);

        using var pdf = PdfDocument.Open(bytes);
        var letters = pdf.GetPage(1).Letters;
        Assert.True(letters.First().FontSize > letters.Min(letter => letter.FontSize));
        Assert.Contains("Synthetic QA specialist.", pdf.GetPage(1).Text);
    }

    [Fact]
    public void Pdf_keeps_content_beyond_the_first_page()
    {
        var content = string.Join('\n', Enumerable.Range(1, 60).Select(index => $"Evidence line {index}"));
        var bytes = new CareerDocumentService().ToPdf(content);
        var text = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.Contains("/Count 2", text);
        Assert.Contains("Evidence line 60", text);
        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(2, pdf.NumberOfPages);
        Assert.Contains("Evidence line 60", pdf.GetPage(2).Text);
    }

    [Fact]
    public void Pdf_starts_a_new_page_with_the_whole_evidence_item()
    {
        var content = string.Join('\n', Enumerable.Range(1, 30).Select(index =>
            $"• Synthetic evidence item {index}: reviewed applicant workflows, documented the result, and checked that the exported record remained readable across a page boundary."));
        var bytes = new CareerDocumentService().ToPdf(content);

        using var pdf = PdfDocument.Open(bytes);
        Assert.True(pdf.NumberOfPages > 1);
        Assert.StartsWith("• Synthetic evidence item", pdf.GetPage(2).Text);
        Assert.Contains("Synthetic evidence item 30", pdf.GetPage(pdf.NumberOfPages).Text);
    }
}
