using ApplicationFoundry.Data;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace ApplicationFoundry.Features.Documents;

public interface ICareerDocumentService
{
    string DraftResume(CandidateProfile profile, JobOpportunity job);
    string DraftCoverLetter(CandidateProfile profile, JobOpportunity job);
    byte[] ToDocx(string content);
    byte[] ToPdf(string content);
}

public sealed class CareerDocumentService : ICareerDocumentService
{
    public string DraftResume(CandidateProfile profile, JobOpportunity job)
    {
        var evidence = profile.Evidence
            .OrderByDescending(item => KeywordHits(item, job.Description))
            .Take(8)
            .Select(item => $"• {item.Claim}{(string.IsNullOrWhiteSpace(item.Details) ? "" : $": {item.Details}")}");
        return $"{profile.DisplayName}\n{profile.Email} | {profile.Phone}\n\nPROFILE\n{profile.Summary}\n\nSELECTED EVIDENCE\n{string.Join('\n', evidence)}";
    }

    public string DraftCoverLetter(CandidateProfile profile, JobOpportunity job)
    {
        var examples = profile.Evidence
            .OrderByDescending(item => KeywordHits(item, job.Description))
            .Take(3)
            .Select(item => item.Claim);
        return $"Dear Hiring Team,\n\nI am interested in the {job.RoleTitle} opportunity. {profile.Summary}\n\nRelevant evidence includes {string.Join("; ", examples)}.\n\nI would welcome a conversation about the role.\n\nSincerely,\n{profile.DisplayName}";
    }

    public byte[] ToDocx(string content)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            WriteEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            var paragraphs = string.Join("", content.Replace("\r", "").Split('\n').Select(line => $"<w:p><w:r><w:t xml:space=\"preserve\">{SecurityElement.Escape(line)}</w:t></w:r></w:p>"));
            WriteEntry(archive, "word/document.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>{paragraphs}<w:sectPr/></w:body></w:document>");
        }
        return output.ToArray();
    }

    public byte[] ToPdf(string content)
    {
        var lines = content.Replace("\r", "").Split('\n')
            .SelectMany(WrapPdfLine)
            .Select(line => EscapePdf(ToWinAnsi(line))).ToArray();
        var pages = lines.Chunk(48).ToArray();
        var fontId = 3 + pages.Length * 2;
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', Enumerable.Range(0, pages.Length).Select(index => $"{3 + index * 2} 0 R"))}] /Count {pages.Length} >>"
        };
        for (var pageIndex = 0; pageIndex < pages.Length; pageIndex++)
        {
            var contentId = 4 + pageIndex * 2;
            var body = new StringBuilder("BT /F1 10 Tf 54 738 Td 13 TL ");
            foreach (var line in pages[pageIndex]) body.Append('(').Append(line).Append(") Tj T* ");
            body.Append("ET");
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {fontId} 0 R >> >> /Contents {contentId} 0 R >>");
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(body.ToString())} >>\nstream\n{body}\nendstream");
        }
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, Encoding.Latin1, 1024, true) { NewLine = "\n" };
        writer.WriteLine("%PDF-1.4"); writer.Flush();
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position);
            writer.WriteLine($"{index + 1} 0 obj"); writer.WriteLine(objects[index]); writer.WriteLine("endobj"); writer.Flush();
        }
        var xref = output.Position;
        writer.WriteLine($"xref\n0 {objects.Count + 1}\n0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1)) writer.WriteLine($"{offset:0000000000} 00000 n ");
        writer.WriteLine($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        writer.Flush();
        return output.ToArray();
    }

    private static int KeywordHits(EvidenceItem item, string description) =>
        item.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(keyword => description.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string EscapePdf(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static IEnumerable<string> WrapPdfLine(string line)
    {
        const int maxCharacters = 85;
        var remaining = line.TrimEnd();
        while (remaining.Length > maxCharacters)
        {
            var breakAt = remaining.LastIndexOf(' ', maxCharacters);
            if (breakAt <= 0) breakAt = maxCharacters;
            yield return remaining[..breakAt].TrimEnd();
            remaining = remaining[breakAt..].TrimStart();
        }
        yield return remaining;
    }

    // WinAnsi uses Windows-1252 positions for punctuation above ASCII. Latin1
    // writes those byte values without introducing a platform code-page dependency.
    private static string ToWinAnsi(string value) => value
        .Replace('•', '\u0095').Replace('–', '\u0096').Replace('—', '\u0097')
        .Replace('‘', '\u0091').Replace('’', '\u0092')
        .Replace('“', '\u0093').Replace('”', '\u0094');
}
