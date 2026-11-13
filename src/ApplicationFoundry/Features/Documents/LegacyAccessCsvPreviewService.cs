using System.Text;
using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;

namespace ApplicationFoundry.Features.Documents;

public sealed record LegacyAccessCsvPreview(
    string FileName, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> SampleRows, int RowCount)
{
    public string SourceSha256 { get; init; } = "";
    internal IReadOnlyList<IReadOnlyList<string>> AllRows { get; init; } = [];
}

public sealed record LegacyAccessMappingPreview(
    IReadOnlyList<string> TargetFields, IReadOnlyList<IReadOnlyDictionary<string, string>> SampleRows, int RowCount);

public sealed record LegacyEvidenceImportPlan(
    string FileName, string SourceSha256, IReadOnlyList<string> Claims, int EmptyOrDuplicateRows);

public sealed record LegacyProfileImportPlan(
    string FileName, string SourceSha256, IReadOnlyDictionary<string, string> Fields);

public sealed class LegacyAccessCsvPreviewException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class LegacyAccessCsvPreviewService
{
    public const int MaxFileBytes = 8 * 1024 * 1024;
    public const string Utf8EncodingName = "utf8";
    public const string Utf16LeEncodingName = "utf16-le";
    public const string Windows1252EncodingName = "windows-1252";
    private const int MaxColumns = 40;
    private const int MaxRows = 5000;
    private static readonly HashSet<string> AllowedTargets =
        ["title", "first-name", "middle-initials", "last-name", "suffix",
            "preferred-display-name", "email", "phone", "summary", "evidence-claim"];

    public LegacyAccessCsvPreview Preview(string fileName, byte[] bytes, string encodingName = Utf8EncodingName)
    {
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 200
            || (!safeName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                && !safeName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
            throw new LegacyAccessCsvPreviewException("FILE_NAME");
        if (bytes.Length is 0 or > MaxFileBytes)
            throw new LegacyAccessCsvPreviewException("SIZE_LIMIT");

        string text;
        try
        {
            text = encodingName switch
            {
                Utf8EncodingName => new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
                Utf16LeEncodingName => ReadUtf16Le(bytes),
                Windows1252EncodingName => (CodePagesEncodingProvider.Instance.GetEncoding(1252)
                    ?? throw new LegacyAccessCsvPreviewException("ENCODING")).GetString(bytes),
                _ => throw new LegacyAccessCsvPreviewException("ENCODING")
            };
        }
        catch (DecoderFallbackException)
        {
            throw new LegacyAccessCsvPreviewException(
                encodingName == Utf8EncodingName ? "UTF8_REQUIRED" : "ENCODING");
        }
        var rows = ParseRows(text);
        if (rows.Count < 2) throw new LegacyAccessCsvPreviewException("NO_RECORDS");
        var columns = rows[0].Select(column => column.Trim()).ToArray();
        if (columns.Length is 0 or > MaxColumns || columns.Any(string.IsNullOrWhiteSpace)
            || columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length)
            throw new LegacyAccessCsvPreviewException("COLUMNS");
        if (rows.Skip(1).Any(row => row.Count != columns.Length))
            throw new LegacyAccessCsvPreviewException("ROW_WIDTH");
        return new LegacyAccessCsvPreview(safeName, columns, rows.Skip(1).Take(5).ToArray(), rows.Count - 1)
        {
            SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            AllRows = rows.Skip(1).ToArray()
        };
    }

    private static string ReadUtf16Le(byte[] bytes)
    {
        if (bytes.Length % 2 != 0 ||
            (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF))
            throw new LegacyAccessCsvPreviewException("ENCODING");
        return new UnicodeEncoding(false, true, true).GetString(bytes).TrimStart('\uFEFF');
    }

    public LegacyAccessMappingPreview PreviewMapping(LegacyAccessCsvPreview source, IReadOnlyList<string> targets)
    {
        if (targets.Count != source.Columns.Count || targets.Any(target => target.Length > 0 && !AllowedTargets.Contains(target)))
            throw new LegacyAccessCsvPreviewException("MAPPING_TARGET");
        var selected = targets.Where(target => target.Length > 0).ToArray();
        if (selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
            throw new LegacyAccessCsvPreviewException("MAPPING_DUPLICATE");
        if (selected.Length == 0)
            throw new LegacyAccessCsvPreviewException("MAPPING_EMPTY");

        var samples = source.SampleRows.Select(row =>
        {
            var mapped = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < targets.Count; index++)
                if (targets[index].Length > 0) mapped.Add(targets[index], row[index]);
            return (IReadOnlyDictionary<string, string>)mapped;
        }).ToArray();
        return new LegacyAccessMappingPreview(selected, samples, source.RowCount);
    }

    public LegacyEvidenceImportPlan PrepareEvidenceImport(LegacyAccessCsvPreview source, IReadOnlyList<string> targets)
    {
        PreviewMapping(source, targets);
        if (targets.Count(target => target == "evidence-claim") != 1
            || targets.Any(target => target.Length > 0 && target != "evidence-claim"))
            throw new LegacyAccessCsvPreviewException("EVIDENCE_MAPPING");

        var index = targets.Select((target, columnIndex) => (target, columnIndex))
            .First(pair => pair.target == "evidence-claim").columnIndex;
        var claims = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;
        foreach (var row in source.AllRows)
        {
            var claim = row[index].Trim();
            if (claim.Length > 240) throw new LegacyAccessCsvPreviewException("CLAIM_LENGTH");
            if (claim.Length == 0 || !seen.Add(claim)) { skipped++; continue; }
            claims.Add(claim);
        }
        if (claims.Count == 0) throw new LegacyAccessCsvPreviewException("NO_CLAIMS");
        return new LegacyEvidenceImportPlan(source.FileName, source.SourceSha256, claims, skipped);
    }

    public LegacyProfileImportPlan PrepareProfileImport(LegacyAccessCsvPreview source, IReadOnlyList<string> targets)
    {
        PreviewMapping(source, targets);
        if (source.RowCount != 1) throw new LegacyAccessCsvPreviewException("PROFILE_ROW_COUNT");
        if (targets.Any(target => target == "evidence-claim"))
            throw new LegacyAccessCsvPreviewException("PROFILE_MAPPING");

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            if (target.Length == 0) continue;
            var value = source.AllRows[0][index].Trim();
            if (value.Length == 0) continue;
            var limit = target switch
            {
                "title" or "suffix" => 32,
                "first-name" or "last-name" => 80,
                "middle-initials" => 40,
                "preferred-display-name" => 160,
                "email" => 256,
                "phone" => 64,
                "summary" => 4000,
                _ => throw new LegacyAccessCsvPreviewException("PROFILE_MAPPING")
            };
            if (value.Length > limit) throw new LegacyAccessCsvPreviewException("PROFILE_LENGTH");
            if (target == "email" && !new EmailAddressAttribute().IsValid(value))
                throw new LegacyAccessCsvPreviewException("PROFILE_EMAIL");
            fields.Add(target, value);
        }
        if (fields.Count == 0) throw new LegacyAccessCsvPreviewException("PROFILE_EMPTY");
        return new LegacyProfileImportPlan(source.FileName, source.SourceSha256, fields);
    }

    private static List<IReadOnlyList<string>> ParseRows(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var afterQuote = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (quoted) { quoted = false; afterQuote = true; }
                else if (field.Length == 0 && !afterQuote) quoted = true;
                else throw new LegacyAccessCsvPreviewException("CSV_FORMAT");
                continue;
            }
            if (quoted) { field.Append(character); continue; }
            if (character is ',' or '\r' or '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                afterQuote = false;
                if (character != ',')
                {
                    if (row.Count > MaxColumns || rows.Count > MaxRows)
                        throw new LegacyAccessCsvPreviewException("LIMIT");
                    rows.Add(row);
                    row = [];
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                }
            }
            else
            {
                if (afterQuote) throw new LegacyAccessCsvPreviewException("CSV_FORMAT");
                field.Append(character);
            }
        }
        if (quoted) throw new LegacyAccessCsvPreviewException("CSV_FORMAT");
        if (field.Length > 0 || row.Count > 0 || afterQuote)
        {
            row.Add(field.ToString());
            if (row.Count > MaxColumns || rows.Count > MaxRows)
                throw new LegacyAccessCsvPreviewException("LIMIT");
            rows.Add(row);
        }
        return rows;
    }
}
