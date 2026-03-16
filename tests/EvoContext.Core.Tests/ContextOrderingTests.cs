using EvoContext.Core.Retrieval;
using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests;

public sealed class ContextOrderingTests
{
    [Fact]
    public void Select_PreservesRankedOrder()
    {
        var candidates = new List<RetrievalCandidate>
        {
            Build("q1", 1, 0.90f, "b-doc", "b-2", 2),
            Build("q1", 2, 0.90f, "a-doc", "a-3", 3),
            Build("q1", 3, 0.90f, "a-doc", "a-1", 1),
            Build("q1", 4, 0.80f, "a-doc", "a-0", 0),
            Build("q1", 5, 0.95f, "c-doc", "c-5", 5)
        };

        var selector = new ContextSelector();

        var ordered = selector.Select(candidates, candidates.Count);

        Assert.Collection(
            ordered,
            first => Assert.Equal("b-2", first.ChunkId),
            second => Assert.Equal("a-3", second.ChunkId),
            third => Assert.Equal("a-1", third.ChunkId),
            fourth => Assert.Equal("a-0", fourth.ChunkId),
            fifth => Assert.Equal("c-5", fifth.ChunkId));
    }

    private static RetrievalCandidate Build(
        string queryId,
        int rank,
        float score,
        string documentId,
        string chunkId,
        int chunkIndex)
    {
        return new RetrievalCandidate(
            queryId,
            rank,
            score,
            score,
            documentId,
            chunkId,
            chunkIndex,
            "text");
    }
}
