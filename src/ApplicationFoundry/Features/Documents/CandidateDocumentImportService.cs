using DocumentExtraction;

namespace ApplicationFoundry.Features.Documents;

public sealed record CandidateDocumentImport(
    string FileName, string Kind, string MediaType, string SourceSha256,
    string ExtractedText, string ReviewNotes);

public interface ICandidateDocumentImportService
{
    Task<CandidateDocumentImport> ExtractAsync(string fileName, string kind, byte[] bytes, CancellationToken cancellationToken = default);
}

public sealed class CandidateDocumentImportService : ICandidateDocumentImportService
{
    public const int MaxFileBytes = 2_097_152;
    private readonly DocumentProcessor processor = new();

    public async Task<CandidateDocumentImport> ExtractAsync(
        string fileName, string kind, byte[] bytes, CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 200)
            throw new ArgumentException("Choose a file with a name of 200 characters or fewer.", nameof(fileName));
        if (kind is not ("resume" or "cover-letter" or "other"))
            throw new ArgumentException("Choose a document kind.", nameof(kind));
        var mediaType = Path.GetExtension(safeName).ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".pdf" => "application/pdf",
            _ => throw new ArgumentException("Choose a TXT, DOCX, or PDF file.", nameof(fileName))
        };
        var result = await processor.ProcessAsync(bytes, mediaType, cancellationToken);
        var text = mediaType == "application/pdf"
            ? string.Join("\n\n", result.Blocks.GroupBy(block => block.Source.Page)
                .Select(page => string.Join(' ', page.Select(block => block.Text))))
            : string.Join('\n', result.Blocks.Select(block => block.Text));
        if (string.IsNullOrWhiteSpace(text))
            throw new DocumentReadException("NO_TEXT");
        var notes = string.Join(", ", result.Diagnostics
            .Where(diagnostic => diagnostic.Code != "HUMAN_REVIEW_REQUIRED")
            .Select(diagnostic => diagnostic.Code));
        return new(safeName, kind, mediaType, result.SourceSha256, text, notes);
    }
}
