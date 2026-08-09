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
        var lines = content.Replace("\r", "").Split('\n').Take(48).Select(EscapePdf).ToArray();
        var body = new StringBuilder("BT /F1 10 Tf 54 738 Td 13 TL ");
        foreach (var line in lines) body.Append('(').Append(line).Append(") Tj T* ");
        body.Append("ET");
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(body.ToString())} >>\nstream\n{body}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, Encoding.ASCII, 1024, true) { NewLine = "\n" };
        writer.WriteLine("%PDF-1.4"); writer.Flush();
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(output.Position);
            writer.WriteLine($"{index + 1} 0 obj"); writer.WriteLine(objects[index]); writer.WriteLine("endobj"); writer.Flush();
        }
        var xref = output.Position;
        writer.WriteLine($"xref\n0 {objects.Length + 1}\n0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1)) writer.WriteLine($"{offset:0000000000} 00000 n ");
        writer.WriteLine($"trailer << /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
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
}
