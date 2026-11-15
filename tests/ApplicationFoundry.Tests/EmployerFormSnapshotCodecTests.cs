using ApplicationFoundry.Features.Forms;

namespace ApplicationFoundry.Tests;

public sealed class EmployerFormSnapshotCodecTests
{
    [Fact]
    public void Parses_synthetic_structure_without_values()
    {
        const string json = """
            {"url":"https://example.test/apply","fields":[
              {"key":"email","label":"Email","type":"email","required":true},
              {"key":"availability","label":"Availability","type":"select","required":true,"options":["Now","Two weeks"]}
            ]}
            """;

        Assert.True(EmployerFormSnapshotCodec.TryParse(json, out var snapshot, out var error), error);
        Assert.Equal("example.test", snapshot!.Url.Host);
        Assert.Equal("Two weeks", snapshot.Fields[1].Options![1]);
    }

    [Theory]
    [InlineData("{\"url\":\"http://example.test/apply\",\"fields\":[{\"key\":\"email\",\"label\":\"Email\",\"type\":\"email\",\"required\":true}]}")]
    [InlineData("{\"url\":\"https://example.test/apply\",\"fields\":[{\"key\":\"email\",\"label\":\"Email\",\"type\":\"email\",\"required\":true,\"value\":\"secret\"}]}")]
    [InlineData("{\"url\":\"https://example.test/apply\",\"fields\":[]}")]
    [InlineData("{\"url\":\"https://example.test/apply\",\"fields\":[{\"key\":\"email\",\"label\":\"Email\",\"type\":\"email\",\"required\":\"yes\"}]}")]
    public void Rejects_untrusted_or_incomplete_snapshot(string json)
    {
        Assert.False(EmployerFormSnapshotCodec.TryParse(json, out var snapshot, out _));
        Assert.Null(snapshot);
    }

    [Fact]
    public void Rejects_oversized_snapshot()
    {
        Assert.False(EmployerFormSnapshotCodec.TryParse(new string('x', EmployerFormSnapshotCodec.MaxJsonCharacters + 1), out _, out _));
    }
}
