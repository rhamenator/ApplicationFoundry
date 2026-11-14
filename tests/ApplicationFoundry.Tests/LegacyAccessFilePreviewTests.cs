using System.Security.Cryptography;
using ApplicationFoundry.Features.Documents;

namespace ApplicationFoundry.Tests;

public sealed class LegacyAccessFilePreviewTests
{
    private static readonly byte[] SyntheticDatabase = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic-jet4.mdb"));
    private static readonly byte[] SyntheticAceDatabase = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic-ace12.accdb"));
    private static readonly byte[] SyntheticJet3Database = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic-jet3-jackcess.mdb"));
    private readonly LegacyAccessFilePreviewService previewer = new();

    [Fact]
    public void Lists_and_previews_a_synthetic_jet4_table_without_importing_records()
    {
        var catalog = previewer.Inspect("C:\\private\\synthetic-jet4.mdb", SyntheticDatabase);
        Assert.Equal("synthetic-jet4.mdb", catalog.FileName);
        Assert.Equal(["Applicants"], catalog.Tables);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(SyntheticDatabase)), catalog.SourceSha256);

        var preview = previewer.Preview(catalog.FileName, SyntheticDatabase, "Applicants");
        Assert.Equal("synthetic-jet4.mdb (Applicants)", preview.FileName);
        Assert.Equal(["Id", "Name", "Email"], preview.Columns);
        Assert.Equal(1, preview.RowCount);
        Assert.Equal(["1", "Avery Example", "avery@example.test"], Assert.Single(preview.SampleRows));

        var mapping = new LegacyAccessCsvPreviewService();
        var plan = mapping.PrepareProfileImport(preview, ["", "first-name", "email"]);
        Assert.Equal("Avery Example", plan.Fields["first-name"]);
        Assert.Equal("avery@example.test", plan.Fields["email"]);
    }

    [Fact]
    public void Rejects_an_unknown_table_and_unreadable_or_mislabeled_files()
    {
        Assert.Equal("TABLE_NOT_FOUND", Assert.Throws<LegacyAccessFilePreviewException>(() =>
            previewer.Preview("test.mdb", SyntheticDatabase, "Missing")).Code);
        Assert.Equal("ACCESS_UNREADABLE", Assert.Throws<LegacyAccessFilePreviewException>(() =>
            previewer.Inspect("test.mdb", new byte[4096])).Code);
        Assert.Equal("FILE_NAME", Assert.Throws<LegacyAccessFilePreviewException>(() =>
            previewer.Inspect("test.csv", SyntheticDatabase)).Code);
    }

    [Fact]
    public void Lists_and_previews_a_synthetic_true_ace12_table()
    {
        var catalog = previewer.Inspect("synthetic-ace12.accdb", SyntheticAceDatabase);
        Assert.Equal(["Applicants"], catalog.Tables);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(SyntheticAceDatabase)), catalog.SourceSha256);

        var preview = previewer.Preview(catalog.FileName, SyntheticAceDatabase, "Applicants");
        Assert.Equal(["Id", "Name", "Email"], preview.Columns);
        Assert.Equal(1, preview.RowCount);
        Assert.Equal(["1", "Avery Example", "avery@example.test"], Assert.Single(preview.SampleRows));
    }

    [Fact]
    public void Lists_and_previews_a_synthetic_jet3_table()
    {
        var catalog = previewer.Inspect("synthetic-jet3-jackcess.mdb", SyntheticJet3Database);
        Assert.Equal(["Table1", "Table2", "Table3", "Table4"], catalog.Tables);

        var preview = previewer.Preview(catalog.FileName, SyntheticJet3Database, "Table1");
        Assert.Equal(["A", "B", "C", "D", "E", "F", "G", "H", "I"], preview.Columns);
        Assert.Equal(2, preview.RowCount);
        Assert.Equal("a", preview.SampleRows[0][0]);
        Assert.Equal("b", preview.SampleRows[0][1]);
    }
}
