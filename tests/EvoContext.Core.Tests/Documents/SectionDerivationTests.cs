using EvoContext.Core.Documents;

namespace EvoContext.Core.Tests.Documents;

public sealed class SectionDerivationTests
{
    [Fact]
    public void CreateChunks_PrefersNearestPrecedingH3_OverH2()
    {
        var headingH2 = "## Billing Rules\n";
        var headingH3 = "\n### Cooling-Off Window\n";
        var text = "# Refund Policy\n\n"
            + headingH2
            + new string('A', 40)
            + headingH3
            + new string('B', 420);

        var chunks = DocumentChunking.CreateChunks(
            "01",
            text,
            chunkSizeChars: 120,
            chunkOverlapChars: 0,
            documentTitle: "Refund Policy");

        var chunkAfterH3 = chunks[^1];

        Assert.Equal("Cooling-Off Window", chunkAfterH3.Section);
    }

    [Fact]
    public void CreateChunks_UsesNearestPrecedingH2_WhenNoPrecedingH3Exists()
    {
        var text = "# Refund Policy\n\n## Annual Plan\n" + new string('A', 400);

        var chunks = DocumentChunking.CreateChunks(
            "02",
            text,
            chunkSizeChars: 120,
            chunkOverlapChars: 0,
            documentTitle: "Refund Policy");

        Assert.Contains(chunks.Skip(1), chunk => string.Equals(chunk.Section, "Annual Plan", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateChunks_DoesNotTreatInlineHashesAsHeadings()
    {
        var text = "# Runbook 502\n\nParagraph with inline ### not a heading marker.\n\n" + new string('A', 280);

        var chunks = DocumentChunking.CreateChunks(
            "03",
            text,
            chunkSizeChars: 100,
            chunkOverlapChars: 0,
            documentTitle: "Runbook 502");

        Assert.DoesNotContain(chunks, chunk => string.Equals(chunk.Section, "not a heading marker.", StringComparison.Ordinal));
        Assert.All(chunks, chunk => Assert.Equal("Runbook 502", chunk.Section));
    }
}
