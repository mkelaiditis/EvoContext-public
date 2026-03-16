using System;
using System.Linq;
using System.Threading.Tasks;
using EvoContext.Core.Documents;
using EvoContext.Core.Tests.Baselines;

namespace EvoContext.Core.Tests;

public sealed class IngestionRegressionTests
{
    private const int ChunkSizeChars = 1200;
    private const int ChunkOverlapChars = 200;

    [Fact]
    public async Task IngestAsync_MatchesGateABaseline()
    {
        var baseline = GateAIngestionBaseline.Load();

        var service = new DocumentIngestionService();
        var result = await service.IngestAsync(
            TestDatasetPaths.PolicyDocsPath,
            ChunkSizeChars,
            ChunkOverlapChars,
            TestContext.Current.CancellationToken);

        var expectedChunks = baseline.Chunks
            .OrderBy(chunk => chunk.DocId, StringComparer.Ordinal)
            .ThenBy(chunk => chunk.ChunkIndex)
            .ToList();

        var actualChunks = result.Chunks
            .OrderBy(chunk => chunk.DocumentId, StringComparer.Ordinal)
            .ThenBy(chunk => chunk.ChunkIndex)
            .ToList();

        if (expectedChunks.Count != actualChunks.Count)
        {
            Assert.Fail(
                $"Chunk count mismatch. expected={expectedChunks.Count} actual={actualChunks.Count}");
        }

        for (var index = 0; index < expectedChunks.Count; index++)
        {
            var expected = expectedChunks[index];
            var actual = actualChunks[index];

            if (!string.Equals(expected.DocId, actual.DocumentId, StringComparison.Ordinal))
            {
                Fail(expected, actual, "doc_id", expected.DocId, actual.DocumentId);
            }

            if (expected.ChunkIndex != actual.ChunkIndex)
            {
                Fail(expected, actual, "chunk_index", expected.ChunkIndex.ToString(), actual.ChunkIndex.ToString());
            }

            if (!string.Equals(expected.ChunkId, actual.ChunkId, StringComparison.Ordinal))
            {
                Fail(expected, actual, "chunk_id", expected.ChunkId, actual.ChunkId);
            }

            if (expected.StartChar != actual.StartChar)
            {
                Fail(expected, actual, "start_char", expected.StartChar.ToString(), actual.StartChar.ToString());
            }

            if (expected.EndChar != actual.EndChar)
            {
                Fail(expected, actual, "end_char", expected.EndChar.ToString(), actual.EndChar.ToString());
            }

            if (!string.Equals(expected.Text, actual.Text, StringComparison.Ordinal))
            {
                Fail(
                    expected,
                    actual,
                    "text",
                    expected.Text.Length.ToString(),
                    actual.Text.Length.ToString());
            }
        }
    }

    private static void Fail(
        BaselineChunk expected,
        DocumentChunk actual,
        string field,
        string expectedValue,
        string actualValue)
    {
        Assert.Fail(
            $"Mismatch at doc_id={expected.DocId} chunk_index={expected.ChunkIndex} field={field} expected={expectedValue} actual={actualValue}");
    }
}
