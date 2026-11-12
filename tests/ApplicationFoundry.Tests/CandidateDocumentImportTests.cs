using System.Text;
using ApplicationFoundry.Features.Documents;
using DocumentExtraction;

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
        Assert.Contains("LAYOUT_REVIEW", pdf.ReviewNotes);
    }

    [Fact]
    public async Task Rejects_unsupported_and_mislabeled_files()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => importer.ExtractAsync("resume.doc", "resume", "old"u8.ToArray()));
        var error = await Assert.ThrowsAsync<DocumentReadException>(() => importer.ExtractAsync("resume.pdf", "resume", "not a PDF"u8.ToArray()));
        Assert.Equal("PDF_SIGNATURE", error.Code);
    }
}
