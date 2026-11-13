using System.Text;
using ApplicationFoundry.Features.Documents;

namespace ApplicationFoundry.Tests;

public sealed class LegacyAccessCsvPreviewTests
{
    private readonly LegacyAccessCsvPreviewService previewer = new();

    [Fact]
    public void PreviewsUtf8AccessExportWithQuotedFieldsWithoutSavingSourceBytes()
    {
        const string csv = "\uFEFFFirstName,LastName,Summary\r\nJordan,Example,\"Reviewed jobs, forms\nand documents\"\r\nAvery,Rivera,\"Built \"\"reliable\"\" systems\"\r\n";

        var preview = previewer.Preview("C:\\private\\applicants.csv", Encoding.UTF8.GetBytes(csv));

        Assert.Equal("applicants.csv", preview.FileName);
        Assert.Equal(2, preview.RowCount);
        Assert.Equal(["FirstName", "LastName", "Summary"], preview.Columns);
        Assert.Equal("Reviewed jobs, forms\nand documents", preview.SampleRows[0][2]);
        Assert.Equal("Built \"reliable\" systems", preview.SampleRows[1][2]);
    }

    [Fact]
    public void RejectsAmbiguousOrMalformedTablesBeforeMapping()
    {
        var duplicate = Assert.Throws<LegacyAccessCsvPreviewException>(() => previewer.Preview(
            "export.csv", "Name,name\nJordan,Example"u8.ToArray()));
        var uneven = Assert.Throws<LegacyAccessCsvPreviewException>(() => previewer.Preview(
            "export.csv", "First,Last\nJordan"u8.ToArray()));
        var unclosed = Assert.Throws<LegacyAccessCsvPreviewException>(() => previewer.Preview(
            "export.csv", "First,Last\n\"Jordan,Example"u8.ToArray()));

        Assert.Equal("COLUMNS", duplicate.Code);
        Assert.Equal("ROW_WIDTH", uneven.Code);
        Assert.Equal("CSV_FORMAT", unclosed.Code);
    }

    [Fact]
    public void ShowsOnlyFiveSampleRowsButCountsAllRecords()
    {
        var csv = "Name\n" + string.Join('\n', Enumerable.Range(1, 8).Select(index => $"Synthetic {index}"));

        var preview = previewer.Preview("export.csv", Encoding.UTF8.GetBytes(csv));

        Assert.Equal(8, preview.RowCount);
        Assert.Equal(5, preview.SampleRows.Count);
        Assert.Equal("Synthetic 1", preview.SampleRows[0][0]);
        Assert.Equal("Synthetic 5", preview.SampleRows[4][0]);
    }

    [Fact]
    public void ExplicitMappingPreviewsSelectedFieldsWithoutChangingSource()
    {
        var source = previewer.Preview("export.csv", "Given,Family,Contact\nJordan,Example,jordan@example.test"u8.ToArray());

        var mapping = previewer.PreviewMapping(source, ["first-name", "last-name", "email"]);

        Assert.Equal(["first-name", "last-name", "email"], mapping.TargetFields);
        Assert.Equal("Jordan", mapping.SampleRows[0]["first-name"]);
        Assert.Equal("Example", mapping.SampleRows[0]["last-name"]);
        Assert.Equal("jordan@example.test", mapping.SampleRows[0]["email"]);
        Assert.Equal("Given", source.Columns[0]);
    }

    [Fact]
    public void RejectsDuplicateOrUnknownMappingTargets()
    {
        var source = previewer.Preview("export.csv", "A,B\nOne,Two"u8.ToArray());

        var duplicate = Assert.Throws<LegacyAccessCsvPreviewException>(() =>
            previewer.PreviewMapping(source, ["email", "email"]));
        var unknown = Assert.Throws<LegacyAccessCsvPreviewException>(() =>
            previewer.PreviewMapping(source, ["email", "unrecognized"]));

        Assert.Equal("MAPPING_DUPLICATE", duplicate.Code);
        Assert.Equal("MAPPING_TARGET", unknown.Code);
    }

    [Fact]
    public void EvidencePlanUsesAllRowsAndReportsDuplicatesWithoutWritingRecords()
    {
        var csv = "Claim,Ignored\n" + string.Join('\n', Enumerable.Range(1, 8)
            .Select(index => $"Synthetic achievement {index},unused")) + "\nsynthetic achievement 1,unused\n,unused";
        var source = previewer.Preview("evidence.csv", Encoding.UTF8.GetBytes(csv));

        var plan = previewer.PrepareEvidenceImport(source, ["evidence-claim", ""]);

        Assert.Equal(8, plan.Claims.Count);
        Assert.Equal(2, plan.EmptyOrDuplicateRows);
        Assert.Equal("Synthetic achievement 8", plan.Claims[7]);
        Assert.Equal(64, plan.SourceSha256.Length);
    }

    [Fact]
    public void EvidencePlanRejectsMixedProfileFieldsAndOverlongClaims()
    {
        var source = previewer.Preview("evidence.csv", "Claim,Name\nSynthetic claim,Jordan"u8.ToArray());
        var tooLong = previewer.Preview("evidence.csv", Encoding.UTF8.GetBytes("Claim\n" + new string('x', 241)));

        var mixed = Assert.Throws<LegacyAccessCsvPreviewException>(() =>
            previewer.PrepareEvidenceImport(source, ["evidence-claim", "first-name"]));
        var length = Assert.Throws<LegacyAccessCsvPreviewException>(() =>
            previewer.PrepareEvidenceImport(tooLong, ["evidence-claim"]));

        Assert.Equal("EVIDENCE_MAPPING", mixed.Code);
        Assert.Equal("CLAIM_LENGTH", length.Code);
    }
}
