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
    public async Task Pdf_export_preserves_evidence_bullets_for_reimport()
    {
        var pdf = await importer.ExtractAsync("resume.pdf", "resume",
            documents.ToPdf("Jordan Example\nSELECTED EVIDENCE\n• Built reliable distributed services"));

        Assert.Contains("• Built reliable distributed services", pdf.ExtractedText);
        Assert.Contains("Built reliable distributed services", CandidateFactMapper.Suggest(pdf.ExtractedText).Claims);
    }

    [Fact]
    public async Task Rejects_unsupported_and_mislabeled_files()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => importer.ExtractAsync("resume.doc", "resume", "old"u8.ToArray()));
        var error = await Assert.ThrowsAsync<DocumentImportException>(() => importer.ExtractAsync("resume.pdf", "resume", "not a PDF"u8.ToArray()));
        Assert.Equal("PDF_SIGNATURE", error.Code);
    }

    [Fact]
    public async Task Distinguishes_empty_documents_from_pdfs_needing_ocr()
    {
        var emptyText = await Assert.ThrowsAsync<DocumentImportException>(() =>
            importer.ExtractAsync("blank.txt", "other", " "u8.ToArray()));
        Assert.Equal("NO_TEXT", emptyText.Code);

        var noTextPdf = await Assert.ThrowsAsync<DocumentImportException>(() =>
            importer.ExtractAsync("scan.pdf", "resume", documents.ToPdf(" ")));
        Assert.Equal("OCR_REQUIRED", noTextPdf.Code);
    }

    [Fact]
    public async Task Uses_local_ocr_only_for_textless_pdfs_and_requires_review()
    {
        var ocr = new SyntheticPdfOcr("Avery Rivera\nVerified synthetic workflow");
        var importerWithOcr = new CandidateDocumentImportService(ocr);
        var textPdf = await importerWithOcr.ExtractAsync("readable.pdf", "resume",
            documents.ToPdf("Avery Rivera\nBuilt reliable systems"));
        Assert.Equal(0, ocr.Calls);
        Assert.Contains("Built reliable systems", textPdf.ExtractedText);

        var scannedBytes = documents.ToPdf(" ");
        var scan = await importerWithOcr.ExtractAsync("scan.pdf", "resume", scannedBytes);
        Assert.Equal(1, ocr.Calls);
        Assert.Equal("Avery Rivera\nVerified synthetic workflow", scan.ExtractedText);
        Assert.Contains("OCR", scan.ReviewNotes);
        Assert.Equal(64, scan.SourceSha256.Length);
    }

    [Fact]
    public async Task Rejects_an_ocr_result_without_text()
    {
        var importerWithOcr = new CandidateDocumentImportService(new SyntheticPdfOcr(" "));
        var error = await Assert.ThrowsAsync<DocumentImportException>(() =>
            importerWithOcr.ExtractAsync("scan.pdf", "resume", documents.ToPdf(" ")));
        Assert.Equal("OCR_NO_TEXT", error.Code);
    }

    [Fact]
    public async Task Reports_ocr_failure_without_exposing_provider_details()
    {
        var importerWithOcr = new CandidateDocumentImportService(new FailingPdfOcr());
        var error = await Assert.ThrowsAsync<DocumentImportException>(() =>
            importerWithOcr.ExtractAsync("scan.pdf", "resume", documents.ToPdf(" ")));
        Assert.Equal("OCR_FAILED", error.Code);
    }

    [Fact]
    public async Task Preserves_caller_cancellation_during_ocr()
    {
        using var cancellation = new CancellationTokenSource();
        var importerWithOcr = new CandidateDocumentImportService(new CancellingPdfOcr(cancellation));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            importerWithOcr.ExtractAsync("scan.pdf", "resume", documents.ToPdf(" "), cancellation.Token));
    }

    private sealed class FailingPdfOcr : ICandidatePdfOcr
    {
        public Task<string> ExtractTextAsync(byte[] pdfBytes, CancellationToken cancellationToken) =>
            throw new IOException("Synthetic provider detail");
    }

    private sealed class CancellingPdfOcr(CancellationTokenSource cancellation) : ICandidatePdfOcr
    {
        public Task<string> ExtractTextAsync(byte[] pdfBytes, CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("");
        }
    }

    private sealed class SyntheticPdfOcr(string text) : ICandidatePdfOcr
    {
        public int Calls { get; private set; }
        public Task<string> ExtractTextAsync(byte[] pdfBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(text);
        }
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
