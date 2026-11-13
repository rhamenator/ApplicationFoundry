using System.Text;

namespace ApplicationFoundry.Features.Documents;

public sealed record LegacyAccessCsvPreview(
    string FileName, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> SampleRows, int RowCount);

public sealed class LegacyAccessCsvPreviewException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class LegacyAccessCsvPreviewService
{
    public const int MaxFileBytes = 8 * 1024 * 1024;
    private const int MaxColumns = 40;
    private const int MaxRows = 5000;

    public LegacyAccessCsvPreview Preview(string fileName, byte[] bytes)
    {
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 200
            || !safeName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new LegacyAccessCsvPreviewException("FILE_NAME");
        if (bytes.Length is 0 or > MaxFileBytes)
            throw new LegacyAccessCsvPreviewException("SIZE_LIMIT");

        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw new LegacyAccessCsvPreviewException("UTF8_REQUIRED"); }
        var rows = ParseRows(text);
        if (rows.Count < 2) throw new LegacyAccessCsvPreviewException("NO_RECORDS");
        var columns = rows[0].Select(column => column.Trim()).ToArray();
        if (columns.Length is 0 or > MaxColumns || columns.Any(string.IsNullOrWhiteSpace)
            || columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length)
            throw new LegacyAccessCsvPreviewException("COLUMNS");
        if (rows.Skip(1).Any(row => row.Count != columns.Length))
            throw new LegacyAccessCsvPreviewException("ROW_WIDTH");
        return new LegacyAccessCsvPreview(safeName, columns, rows.Skip(1).Take(5).ToArray(), rows.Count - 1);
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
