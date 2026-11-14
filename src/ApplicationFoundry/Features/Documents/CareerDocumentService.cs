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
            .Select(item => $"• {FormatEvidence(item)}");
        var contact = string.Join(" | ", new[] { profile.Email, profile.Phone }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var header = string.IsNullOrEmpty(contact) ? profile.DisplayName : $"{profile.DisplayName}\n{contact}";
        return $"{header}\n\nPROFILE\n{profile.Summary}\n\nSELECTED EVIDENCE\n{string.Join('\n', evidence)}";
    }

    public string DraftCoverLetter(CandidateProfile profile, JobOpportunity job)
    {
        var examples = profile.Evidence
            .OrderByDescending(item => KeywordHits(item, job.Description))
            .Take(3)
            .Select(item => $"• {FormatEvidence(item)}")
            .ToArray();
        var contact = string.Join(" | ", new[] { profile.Email, profile.Phone }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var header = string.IsNullOrEmpty(contact) ? profile.DisplayName : $"{profile.DisplayName}\n{contact}";
        var organization = string.IsNullOrWhiteSpace(job.Organization) ? "" : $" at {job.Organization.Trim()}";
        var summary = profile.Summary.Trim();
        if (summary.Length > 0 && !".!?".Contains(summary[^1])) summary += ".";
        var introduction = $"I am applying for the {job.RoleTitle.Trim()} position{organization}.";
        if (summary.Length > 0) introduction += $" {summary}";
        var evidenceSection = examples.Length == 0 ? "" : $"\n\nRelevant experience\n{string.Join('\n', examples)}";
        return $"{header}\n\nDear Hiring Team,\n\n{introduction}{evidenceSection}\n\nI would welcome the opportunity to discuss this position.\n\nSincerely,\n{profile.DisplayName}";
    }

    private static string FormatEvidence(EvidenceItem item)
    {
        var claim = item.Claim.Trim();
        var details = item.Details.Trim();
        if (details.Length == 0) return claim;
        var plainClaim = claim.TrimEnd('.', '!', '?');
        if (details.StartsWith(plainClaim, StringComparison.OrdinalIgnoreCase) &&
            (details.Length == plainClaim.Length || !char.IsLetterOrDigit(details[plainClaim.Length])))
            return details;
        return $"{plainClaim}: {details}";
    }

    public byte[] ToDocx(string content)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            WriteEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            var normalized = content.Replace("\r", "");
            var isResume = normalized.Contains("\nPROFILE\n", StringComparison.Ordinal);
            var isCoverLetter = normalized.Contains("\nDear Hiring Team,\n", StringComparison.Ordinal);
            var paragraphs = string.Join("", normalized.Split('\n')
                .Select((line, index) => ToDocxParagraph(line, index, isResume, isCoverLetter)));
            WriteEntry(archive, "word/document.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>{paragraphs}<w:sectPr/></w:body></w:document>");
        }
        return output.ToArray();
    }

    public byte[] ToPdf(string content)
    {
        var normalized = content.Replace("\r", "");
        var isResume = normalized.Contains("\nPROFILE\n", StringComparison.Ordinal);
        var isCoverLetter = normalized.Contains("\nDear Hiring Team,\n", StringComparison.Ordinal);
        var groups = normalized.Split('\n')
            .Select((line, index) => WrapPdfLine(line, (isResume || isCoverLetter) && index == 0 ? 33_600 : 50_400)
                .Select(part => EscapePdf(ToWinAnsi(part))).ToArray());
        var pages = PaginatePdfLines(groups);
        var fontId = 3 + pages.Length * 2;
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', Enumerable.Range(0, pages.Length).Select(index => $"{3 + index * 2} 0 R"))}] /Count {pages.Length} >>"
        };
        for (var pageIndex = 0; pageIndex < pages.Length; pageIndex++)
        {
            var contentId = 4 + pageIndex * 2;
            var body = new StringBuilder();
            var baseline = 738;
            for (var lineIndex = 0; lineIndex < pages[pageIndex].Length; lineIndex++)
            {
                var line = pages[pageIndex][lineIndex];
                var name = (isResume || isCoverLetter) && pageIndex == 0 && lineIndex == 0;
                var heading = isResume && IsSectionHeading(line) || isCoverLetter && line == "Relevant experience";
                body.Append(name || heading ? "BT /F2 " : "BT /F1 ")
                    .Append(name ? "15" : heading ? "11" : "10")
                    .Append(" Tf 54 ").Append(baseline).Append(" Td (")
                    .Append(line).Append(") Tj ET ");
                baseline -= name ? 18 : 13;
            }
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {fontId} 0 R /F2 {fontId + 1} 0 R >> >> /Contents {contentId} 0 R >>");
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(body.ToString())} >>\nstream\n{body}\nendstream");
        }
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
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

    private static string ToDocxParagraph(string line, int index, bool isResume, bool isCoverLetter)
    {
        var name = (isResume || isCoverLetter) && index == 0;
        var heading = isResume && IsSectionHeading(line) || isCoverLetter && line == "Relevant experience";
        var bullet = (isResume || isCoverLetter) && line.StartsWith("• ", StringComparison.Ordinal);
        var paragraph = name
            ? "<w:pPr><w:spacing w:after=\"80\"/></w:pPr>"
            : heading
                ? "<w:pPr><w:keepNext/><w:spacing w:before=\"160\" w:after=\"80\"/></w:pPr>"
                : bullet ? "<w:pPr><w:keepLines/><w:ind w:left=\"360\" w:hanging=\"180\"/></w:pPr>" : "";
        var run = name ? "<w:rPr><w:b/><w:sz w:val=\"32\"/></w:rPr>"
            : heading ? "<w:rPr><w:b/><w:sz w:val=\"22\"/></w:rPr>" : "";
        return $"<w:p>{paragraph}<w:r>{run}<w:t xml:space=\"preserve\">{SecurityElement.Escape(line)}</w:t></w:r></w:p>";
    }

    private static string EscapePdf(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static bool IsSectionHeading(string line) => line.Length is > 0 and < 50 &&
        line.Any(char.IsLetter) && line.Where(char.IsLetter).All(char.IsUpper);

    private static IEnumerable<string> WrapPdfLine(string line, int maxWidth)
    {
        // Helvetica is drawn at 10 pt across a 504 pt text area. These
        // conservative widths are in thousandths of an em.
        var remaining = line.TrimEnd();
        if (remaining.Length == 0)
        {
            yield return "";
            yield break;
        }
        while (remaining.Length > 0)
        {
            var width = 0;
            var breakAt = 0;
            var lastSpace = -1;
            while (breakAt < remaining.Length && width + PdfGlyphWidth(remaining[breakAt]) <= maxWidth)
            {
                width += PdfGlyphWidth(remaining[breakAt]);
                if (remaining[breakAt] == ' ') lastSpace = breakAt;
                breakAt++;
            }
            if (breakAt == remaining.Length)
            {
                yield return remaining;
                yield break;
            }
            if (lastSpace > 0) breakAt = lastSpace;
            if (breakAt == 0) breakAt = 1;
            yield return remaining[..breakAt].TrimEnd();
            remaining = remaining[breakAt..].TrimStart();
        }
    }

    private static int PdfGlyphWidth(char character) => character switch
    {
        ' ' => 300,
        'i' or 'l' or 'j' or 't' or 'f' or 'r' or 'I' => 400,
        'm' or 'w' or 'M' => 900,
        'W' or '@' or '%' or '—' or '–' => 1_100,
        >= 'A' and <= 'Z' => 750,
        >= 'a' and <= 'z' => 700,
        >= '0' and <= '9' => 600,
        _ => 700
    };

    private static string[][] PaginatePdfLines(IEnumerable<string[]> groups)
    {
        const int linesPerPage = 48;
        var pages = new List<List<string>> { new() };
        foreach (var group in groups)
        {
            if (group.Length <= linesPerPage && pages[^1].Count > 0 &&
                pages[^1].Count + group.Length > linesPerPage)
                pages.Add(new List<string>());
            foreach (var line in group)
            {
                if (pages[^1].Count == linesPerPage) pages.Add(new List<string>());
                pages[^1].Add(line);
            }
        }
        return pages.Select(page => page.ToArray()).ToArray();
    }

    // WinAnsi uses Windows-1252 positions for punctuation above ASCII. Latin1
    // writes those byte values without introducing a platform code-page dependency.
    private static string ToWinAnsi(string value) => value
        .Replace('•', '\u0095').Replace('–', '\u0096').Replace('—', '\u0097')
        .Replace('‘', '\u0091').Replace('’', '\u0092')
        .Replace('“', '\u0093').Replace('”', '\u0094');
}
