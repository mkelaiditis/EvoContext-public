using System.Text;
using EvoContext.Core.Documents;

namespace EvoContext.Core.Tests;

public sealed class IngestionDeterminismTests
{
    private const int ChunkSizeChars = 1200;
    private const int ChunkOverlapChars = 200;

    [Fact]
    public async Task IngestAsync_IsDeterministicAcrossRuns()
    {
        using var tempDir = new TempDirectory();
        var doc1 = Path.Combine(tempDir.Path, "01_subscription_plans_overview.md");
        var doc2 = Path.Combine(tempDir.Path, "02_refund_policy_general_terms.md");

        var content = new string('x', 1500);
        await File.WriteAllTextAsync(doc1, content, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(doc2, content, TestContext.Current.CancellationToken);

        var service = new DocumentIngestionService();
        var first = await service.IngestAsync(
            tempDir.Path,
            ChunkSizeChars,
            ChunkOverlapChars,
            TestContext.Current.CancellationToken);
        var second = await service.IngestAsync(
            tempDir.Path,
            ChunkSizeChars,
            ChunkOverlapChars,
            TestContext.Current.CancellationToken);

        var firstSignature = BuildSignature(first);
        var secondSignature = BuildSignature(second);

        Assert.Equal(firstSignature, secondSignature);
    }

    private static string BuildSignature(IngestResult result)
    {
        var builder = new StringBuilder();
        foreach (var document in result.Documents)
        {
            builder.Append(document.DocId)
                .Append('|')
                .Append(document.NormalizedText.Length)
                .Append('|')
                .Append(document.Title)
                .Append('\n');
        }

        builder.Append("CHUNKS").Append('\n');
        foreach (var chunk in result.Chunks)
        {
            builder.Append(chunk.ChunkId)
                .Append('|')
                .Append(chunk.StartChar)
                .Append('|')
                .Append(chunk.EndChar)
                .Append('|')
                .Append(chunk.Text.Length)
                .Append('\n');
        }

        return builder.ToString();
    }
}
