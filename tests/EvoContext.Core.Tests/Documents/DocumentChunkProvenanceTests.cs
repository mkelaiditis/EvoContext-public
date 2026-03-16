using EvoContext.Core.Documents;

namespace EvoContext.Core.Tests.Documents;

public sealed class DocumentChunkProvenanceTests
{
    [Fact]
    public void CreateChunks_PopulatesDocumentTitle_OnAllChunks()
    {
        var text = "# Refund Policy\n\n" + new string('A', 260);

        var chunks = DocumentChunking.CreateChunks(
            "01",
            text,
            chunkSizeChars: 100,
            chunkOverlapChars: 0,
            documentTitle: "Refund Policy");

        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.Equal("Refund Policy", chunk.DocumentTitle));
    }

    [Fact]
    public void CreateChunks_UsesDocumentTitleAsSectionFallback_WhenNoHeadingsExist()
    {
        var text = "No markdown headings here. " + new string('A', 220);

        var chunks = DocumentChunking.CreateChunks(
            "02",
            text,
            chunkSizeChars: 90,
            chunkOverlapChars: 0,
            documentTitle: "Billing Runbook");

        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.Equal("Billing Runbook", chunk.Section));
    }

    [Fact]
    public void CreateChunks_UsesGeneralSectionFallback_WhenTitleAndHeadingsAreMissing()
    {
        var text = "No headings and no title. " + new string('A', 240);

        var chunks = DocumentChunking.CreateChunks(
            "03",
            text,
            chunkSizeChars: 100,
            chunkOverlapChars: 0,
            documentTitle: null);

        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.Equal("General", chunk.Section));
    }
}
