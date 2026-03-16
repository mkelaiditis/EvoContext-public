using EvoContext.Core.Documents;

namespace EvoContext.Core.Tests;

public sealed class ChunkingTests
{
    private const int ChunkSizeChars = 1200;
    private const int ChunkOverlapChars = 200;

    [Fact]
    public void CreateChunks_ReturnsSingleChunk_WhenTextEqualsChunkSize()
    {
        var text = new string('a', ChunkSizeChars);

        var chunks = DocumentChunking.CreateChunks("01", text, ChunkSizeChars, ChunkOverlapChars);

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.Equal("01_0", chunk.ChunkId);
        Assert.Equal(0, chunk.StartChar);
        Assert.Equal(ChunkSizeChars, chunk.EndChar);
        Assert.Equal(text, chunk.Text);
    }

    [Fact]
    public void CreateChunks_UsesOverlapForSecondChunk()
    {
        var length = ChunkSizeChars + 150;
        var text = new string('b', length);

        var chunks = DocumentChunking.CreateChunks("02", text, ChunkSizeChars, ChunkOverlapChars);

        Assert.Equal(2, chunks.Count);
        var first = chunks[0];
        var second = chunks[1];

        Assert.Equal(0, first.StartChar);
        Assert.Equal(ChunkSizeChars, first.EndChar);
        Assert.Equal(ChunkSizeChars - ChunkOverlapChars, second.StartChar);
        Assert.Equal(length, second.EndChar);
        Assert.Equal(ChunkOverlapChars, first.EndChar - second.StartChar);
        Assert.Equal(text.Substring(second.StartChar, second.EndChar - second.StartChar), second.Text);
    }
}
