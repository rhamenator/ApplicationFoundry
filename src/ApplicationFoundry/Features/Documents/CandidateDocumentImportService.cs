using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ApplicationFoundry.Features.Documents;

public sealed record CandidateDocumentImport(
    string FileName, string Kind, string MediaType, string SourceSha256,
    string ExtractedText, string ReviewNotes);

public sealed class DocumentImportException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public interface ICandidateDocumentImportService
{
    Task<CandidateDocumentImport> ExtractAsync(string fileName, string kind, byte[] bytes, CancellationToken cancellationToken = default);
}

// A desktop host may supply a local, bounded PDF OCR engine. The web build
// deliberately has no provider and continues to report OCR_REQUIRED.
public interface ICandidatePdfOcr
{
    Task<string> ExtractTextAsync(byte[] pdfBytes, CancellationToken cancellationToken);
}

public sealed class CandidateDocumentImportService : ICandidateDocumentImportService
{
    public const int MaxFileBytes = 2_097_152;
    private const int MaxCharacters = 200_000;
    private const int MaxPages = 100;
    private const long MaxExpandedBytes = 20_971_520;
    private readonly ICandidatePdfOcr? pdfOcr;

    public CandidateDocumentImportService(ICandidatePdfOcr? pdfOcr = null) => this.pdfOcr = pdfOcr;

    public async Task<CandidateDocumentImport> ExtractAsync(
        string fileName, string kind, byte[] bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 200)
            throw new ArgumentException("Choose a file with a name of 200 characters or fewer.", nameof(fileName));
        if (kind is not ("resume" or "cover-letter" or "other"))
            throw new ArgumentException("Choose a document kind.", nameof(kind));
        if (bytes.Length is 0 or > MaxFileBytes)
            throw new DocumentImportException("SIZE_LIMIT");

        var (mediaType, text, notes) = Path.GetExtension(safeName).ToLowerInvariant() switch
        {
            ".txt" => ("text/plain", ReadPlainText(bytes), ""),
            ".docx" => ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ReadDocx(bytes, cancellationToken), ""),
            ".pdf" => ("application/pdf", ReadPdf(bytes, cancellationToken), "PDF layout needs review"),
            _ => throw new ArgumentException("Choose a TXT, DOCX, or PDF file.", nameof(fileName))
        };
        if (string.IsNullOrWhiteSpace(text) && mediaType == "application/pdf" && pdfOcr is not null)
        {
            text = await pdfOcr.ExtractTextAsync(bytes, cancellationToken);
            notes = "OCR text and PDF layout need review";
        }
        if (string.IsNullOrWhiteSpace(text))
            throw new DocumentImportException(mediaType == "application/pdf"
                ? pdfOcr is null ? "OCR_REQUIRED" : "OCR_NO_TEXT" : "NO_TEXT");
        if (text.Length > MaxCharacters)
            throw new DocumentImportException("TEXT_LIMIT");
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new CandidateDocumentImport(safeName, kind, mediaType, hash, text, notes);
    }

    private static string ReadPlainText(byte[] bytes)
    {
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            if (text.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
                throw new DocumentImportException("INVALID_TEXT");
            return text.Replace("\r\n", "\n");
        }
        catch (DecoderFallbackException)
        {
            throw new DocumentImportException("INVALID_TEXT");
        }
    }

    private static string ReadDocx(byte[] bytes, CancellationToken cancellationToken)
    {
        try
        {
            using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
            {
                if (archive.Entries.Count > 2_000 || archive.Entries.Sum(entry => entry.Length) > MaxExpandedBytes ||
                    archive.GetEntry("word/document.xml") is null)
                    throw new DocumentImportException("DOCX_LIMIT_OR_FORMAT");
            }
            using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false,
                new OpenSettings { MaxCharactersInPart = MaxExpandedBytes });
            var body = document.MainDocumentPart?.Document?.Body
                ?? throw new DocumentImportException("DOCX_LIMIT_OR_FORMAT");
            var lines = new List<string>();
            var length = 0;
            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = paragraph.InnerText;
                length += line.Length + 1;
                if (length > MaxCharacters) throw new DocumentImportException("TEXT_LIMIT");
                lines.Add(line);
            }
            return string.Join('\n', lines);
        }
        catch (DocumentImportException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new DocumentImportException("MALFORMED_DOCUMENT"); }
    }

    private static string ReadPdf(byte[] bytes, CancellationToken cancellationToken)
    {
        if (!bytes.AsSpan().StartsWith("%PDF-"u8))
            throw new DocumentImportException("PDF_SIGNATURE");
        try
        {
            using var document = PdfDocument.Open(bytes);
            if (document.NumberOfPages > MaxPages) throw new DocumentImportException("PAGE_LIMIT");
            var pages = new List<string>();
            var length = 0;
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = ContentOrderTextExtractor.GetText(page);
                length += text.Length;
                if (length > MaxCharacters) throw new DocumentImportException("TEXT_LIMIT");
                pages.Add(text);
            }
            return string.Join("\n\n", pages);
        }
        catch (DocumentImportException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new DocumentImportException("MALFORMED_DOCUMENT"); }
    }
}
