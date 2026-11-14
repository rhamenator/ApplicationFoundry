using System.Security.Cryptography;
using JetDatabaseReader;

namespace ApplicationFoundry.Features.Documents;

public sealed record LegacyAccessFileCatalog(
    string FileName, string SourceSha256, IReadOnlyList<string> Tables);

public sealed class LegacyAccessFilePreviewException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

// Reads a bounded copy of a selected database. The original file is never opened
// by the parser, and the copy is removed before returning any preview.
public sealed class LegacyAccessFilePreviewService
{
    public const int MaxFileBytes = 8 * 1024 * 1024;
    private const int MaxTables = 100;
    private const int MaxColumns = 40;
    private const int MaxRows = 5000;

    public LegacyAccessFileCatalog Inspect(string fileName, byte[] bytes)
    {
        var safeName = Validate(fileName, bytes);
        return WithReader(bytes, reader =>
        {
            var tables = reader.ListTables();
            if (tables.Count is 0 or > MaxTables)
                throw new LegacyAccessFilePreviewException("TABLE_LIMIT_OR_EMPTY");
            return new LegacyAccessFileCatalog(safeName,
                Convert.ToHexStringLower(SHA256.HashData(bytes)), tables);
        });
    }

    public LegacyAccessCsvPreview Preview(string fileName, byte[] bytes, string tableName)
    {
        var safeName = Validate(fileName, bytes);
        return WithReader(bytes, reader =>
        {
            if (string.IsNullOrWhiteSpace(tableName) || !reader.ListTables().Contains(tableName, StringComparer.Ordinal))
                throw new LegacyAccessFilePreviewException("TABLE_NOT_FOUND");
            var columns = reader.GetColumnMetadata(tableName).Select(column => column.Name).ToArray();
            if (columns.Length is 0 or > MaxColumns || columns.Any(string.IsNullOrWhiteSpace) ||
                columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length)
                throw new LegacyAccessFilePreviewException("COLUMNS");
            var rows = reader.ReadTableAsStrings(tableName, MaxRows + 1).Rows;
            if (rows.Count is 0 or > MaxRows)
                throw new LegacyAccessFilePreviewException("ROW_LIMIT_OR_EMPTY");
            if (rows.Any(row => row.Count != columns.Length || row.Any(cell => cell.Length > 4000)))
                throw new LegacyAccessFilePreviewException("ROW_LIMIT_OR_FORMAT");
            var allRows = rows.Select(row => (IReadOnlyList<string>)row.ToArray()).ToArray();
            return new LegacyAccessCsvPreview($"{safeName} ({tableName})", columns,
                allRows.Take(5).ToArray(), allRows.Length)
            {
                SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                AllRows = allRows
            };
        });
    }

    private static string Validate(string fileName, byte[] bytes)
    {
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 200 ||
            !(safeName.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase) ||
              safeName.EndsWith(".accdb", StringComparison.OrdinalIgnoreCase)))
            throw new LegacyAccessFilePreviewException("FILE_NAME");
        if (bytes.Length is 0 or > MaxFileBytes)
            throw new LegacyAccessFilePreviewException("SIZE_LIMIT");
        return safeName;
    }

    private static T WithReader<T>(byte[] bytes, Func<AccessReader, T> read)
    {
        var path = Path.Combine(Path.GetTempPath(), $"application-foundry-access-{Guid.NewGuid():N}.mdb");
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var file = new FileStream(path, options))
                file.Write(bytes);
            using var reader = AccessReader.Open(path, new AccessReaderOptions
            {
                FileAccess = FileAccess.Read,
                FileShare = FileShare.Read,
                OleObjectMode = OleObjectMode.Placeholder
            });
            return read(reader);
        }
        catch (LegacyAccessFilePreviewException) { throw; }
        catch (Exception) { throw new LegacyAccessFilePreviewException("ACCESS_UNREADABLE"); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
