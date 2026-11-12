using System.Text;
using ApplicationFoundry.Features.Documents;

namespace ApplicationFoundry.Tests;

public sealed class CandidateDocumentImportTests
{
    private readonly CandidateDocumentImportService importer = new();
    private readonly CareerDocumentService documents = new();

    [Fact]
    public async Task Imports_text_and_keeps_a_content_hash_without_original_bytes()
    {
        var result = await importer.ExtractAsync("C:\\private\\resume.txt", "resume",
            Encoding.UTF8.GetBytes("Avery Rivera\nBuilt reliable systems"));

        Assert.Equal("resume.txt", result.FileName);
        Assert.Contains("Built reliable systems", result.ExtractedText);
        Assert.Equal(64, result.SourceSha256.Length);
    }

    [Fact]
    public async Task Imports_docx_and_pdf_generated_by_the_document_service()
    {
        var word = await importer.ExtractAsync("resume.docx", "resume",
            documents.ToDocx("Avery Rivera\nBuilt reliable systems"));
        var pdf = await importer.ExtractAsync("letter.pdf", "cover-letter",
            documents.ToPdf("Avery Rivera\nBuilt reliable systems"));

        Assert.Contains("Built reliable systems", word.ExtractedText);
        Assert.Contains("Avery", pdf.ExtractedText);
        Assert.Contains("layout needs review", pdf.ReviewNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_unsupported_and_mislabeled_files()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => importer.ExtractAsync("resume.doc", "resume", "old"u8.ToArray()));
        var error = await Assert.ThrowsAsync<DocumentImportException>(() => importer.ExtractAsync("resume.pdf", "resume", "not a PDF"u8.ToArray()));
        Assert.Equal("PDF_SIGNATURE", error.Code);
    }

    [Fact]
    public void Suggests_only_reviewable_facts_from_imported_text()
    {
        const string text = "Avery Rivera\navery@example.com | (313) 555-0123\nProfessional Summary\nReliable systems engineer with experience in testing.\n\nExperience\n• Built reliable release pipelines for multiple products\n- Reduced support requests through clearer documentation\n• Built reliable release pipelines for multiple products";

        var suggestions = CandidateFactMapper.Suggest(text);

        Assert.Equal("avery@example.com", suggestions.Email);
        Assert.Equal("(313) 555-0123", suggestions.Phone);
        Assert.Equal("Reliable systems engineer with experience in testing.", suggestions.Summary);
        Assert.Equal(2, suggestions.Claims.Count);
        Assert.Equal("Built reliable release pipelines for multiple products", suggestions.Claims[0]);
    }
}
