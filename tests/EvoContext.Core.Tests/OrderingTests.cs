using EvoContext.Core.Documents;
using EvoContext.Core.Tests.Fixtures;

namespace EvoContext.Core.Tests;

public sealed class OrderingTests
{
    private const int ChunkSizeChars = 1200;
    private const int ChunkOverlapChars = 200;

    [Fact]
    public async Task IngestAsync_SortsDocumentsByDocId()
    {
        using var tempDir = new TempDirectory();
        var doc2 = Path.Combine(tempDir.Path, "02_refund_policy_general_terms.md");
        var doc1 = Path.Combine(tempDir.Path, "01_subscription_plans_overview.md");

        await File.WriteAllTextAsync(doc2, DocumentIngestionFixtures.TextWithCrlf, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(doc1, DocumentIngestionFixtures.TextWithCrlf, TestContext.Current.CancellationToken);

        var service = new DocumentIngestionService();
        var result = await service.IngestAsync(
            tempDir.Path,
            ChunkSizeChars,
            ChunkOverlapChars,
            TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "01", "02" }, result.Documents.Select(document => document.DocId).ToArray());
    }

    [Fact]
    public async Task IngestAsync_SortsChunksByDocIdThenChunkIndex()
    {
        using var tempDir = new TempDirectory();
        var doc2 = Path.Combine(tempDir.Path, "02_refund_policy_general_terms.md");
        var doc1 = Path.Combine(tempDir.Path, "01_subscription_plans_overview.md");

        var text = new string('x', 1500);
        await File.WriteAllTextAsync(doc2, text, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(doc1, text, TestContext.Current.CancellationToken);

        var service = new DocumentIngestionService();
        var result = await service.IngestAsync(
            tempDir.Path,
            ChunkSizeChars,
            ChunkOverlapChars,
            TestContext.Current.CancellationToken);

        var ordered = result.Chunks
            .OrderBy(chunk => chunk.DocumentId, StringComparer.Ordinal)
            .ThenBy(chunk => chunk.ChunkIndex)
            .Select(chunk => $"{chunk.DocumentId}_{chunk.ChunkIndex}")
            .ToArray();

        var actual = result.Chunks.Select(chunk => $"{chunk.DocumentId}_{chunk.ChunkIndex}").ToArray();

        Assert.Equal(ordered, actual);
    }
}
