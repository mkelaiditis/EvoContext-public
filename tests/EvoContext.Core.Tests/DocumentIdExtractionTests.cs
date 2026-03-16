using EvoContext.Core.Documents;
using EvoContext.Core.Tests.Fixtures;

namespace EvoContext.Core.Tests;

public sealed class DocumentIdExtractionTests
{
    private const int ChunkSizeChars = 1200;
    private const int ChunkOverlapChars = 200;

    [Fact]
    public async Task IngestAsync_AssignsDocIdFromTwoDigitPrefix()
    {
        using var tempDir = new TempDirectory();
        var validFile = Path.Combine(tempDir.Path, DocumentIngestionFixtures.ValidFilename);

        await File.WriteAllTextAsync(validFile, DocumentIngestionFixtures.TextWithCrlf, TestContext.Current.CancellationToken);

        var service = new DocumentIngestionService();
        var result = await service.IngestAsync(
            tempDir.Path,
            ChunkSizeChars,
            ChunkOverlapChars,
            TestContext.Current.CancellationToken);

        Assert.Single(result.Documents);
        Assert.Equal("01", result.Documents[0].DocId);
    }

    [Fact]
    public async Task IngestAsync_SkipsInvalidPrefixFiles()
    {
        using var tempDir = new TempDirectory();
        var validFile = Path.Combine(tempDir.Path, DocumentIngestionFixtures.ValidFilename);
        var invalidFile = Path.Combine(tempDir.Path, DocumentIngestionFixtures.InvalidFilename);

        await File.WriteAllTextAsync(validFile, DocumentIngestionFixtures.TextWithCrlf, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(invalidFile, DocumentIngestionFixtures.TextWithCrlf, TestContext.Current.CancellationToken);

        var service = new DocumentIngestionService();
        var result = await service.IngestAsync(
            tempDir.Path,
            ChunkSizeChars,
            ChunkOverlapChars,
            TestContext.Current.CancellationToken);

        Assert.Single(result.Documents);
        Assert.Equal("01", result.Documents[0].DocId);
        Assert.Single(result.SkippedFiles);
        Assert.Contains(DocumentIngestionFixtures.InvalidFilename, result.SkippedFiles);
        Assert.Equal(1, result.DocumentsSkipped);
    }
}
