using EvoContext.Core.Retrieval;
using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests;

public sealed class ContextPackPackerTests
{
    [Fact]
    public void Pack_ReturnsEmptyWhenNoCandidates()
    {
        var packer = new ContextPackPacker(10);

        var pack = packer.Pack(Array.Empty<RetrievalCandidate>());

        Assert.Equal(string.Empty, pack.Text);
        Assert.Equal(0, pack.CharCount);
        Assert.Equal(0, pack.ChunkCount);
    }

    [Fact]
    public void Pack_ReturnsSingleChunkWithinBudget()
    {
        var packer = new ContextPackPacker(10);
        var candidates = new List<RetrievalCandidate>
        {
            Build("a-doc", 0, "short")
        };

        var pack = packer.Pack(candidates);

        Assert.Equal("short", pack.Text);
        Assert.Equal(5, pack.CharCount);
        Assert.Equal(1, pack.ChunkCount);
    }

    [Fact]
    public void Pack_ReturnsEmptyWhenAllChunksExceedBudget()
    {
        var packer = new ContextPackPacker(4);
        var candidates = new List<RetrievalCandidate>
        {
            Build("a-doc", 0, "toolong"),
            Build("b-doc", 1, "alsotoolong")
        };

        var pack = packer.Pack(candidates);

        Assert.Equal(string.Empty, pack.Text);
        Assert.Equal(0, pack.CharCount);
        Assert.Equal(0, pack.ChunkCount);
    }

    [Fact]
    public void Pack_RemovesLowestRankedChunksUntilWithinBudget()
    {
        var packer = new ContextPackPacker(13);
        var candidates = new List<RetrievalCandidate>
        {
            Build("a-doc", 0, new string('a', 5)),
            Build("b-doc", 1, new string('b', 5)),
            Build("c-doc", 2, new string('c', 5))
        };

        var pack = packer.Pack(candidates);

        Assert.Equal("aaaaa\n\nbbbbb", pack.Text);
        Assert.Equal(12, pack.CharCount);
        Assert.Equal(2, pack.ChunkCount);
    }

    [Fact]
    public void Pack_DoesNotTruncate_RemovesOverBudgetChunk()
    {
        var packer = new ContextPackPacker(8);
        var candidates = new List<RetrievalCandidate>
        {
            Build("a-doc", 0, "short"),
            Build("b-doc", 1, "toolongtext")
        };

        var pack = packer.Pack(candidates);

        Assert.Equal("short", pack.Text);
        Assert.Equal(5, pack.CharCount);
        Assert.Equal(1, pack.ChunkCount);
    }

    private static RetrievalCandidate Build(string documentId, int chunkIndex, string text)
    {
        return new RetrievalCandidate(
            "q1",
            0,
            0.50f,
            0.50f,
            documentId,
            $"{documentId}_{chunkIndex}",
            chunkIndex,
            text);
    }
}
