using EvoContext.Core.Retrieval;
using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests;

public sealed class ContextSelectorTests
{
    [Fact]
    public void Select_ReturnsEmptyWhenNoCandidates()
    {
        var selector = new ContextSelector();

        var selected = selector.Select(Array.Empty<RetrievalCandidate>(), 3);

        Assert.Empty(selected);
    }

    [Fact]
    public void Select_ReturnsTopKInOrder()
    {
        var candidates = new List<RetrievalCandidate>
        {
            Build("a-doc", 0),
            Build("b-doc", 1),
            Build("c-doc", 2)
        };

        var selector = new ContextSelector();

        var selected = selector.Select(candidates, 2);

        Assert.Collection(
            selected,
            first => Assert.Equal("a-doc", first.DocumentId),
            second => Assert.Equal("b-doc", second.DocumentId));
    }

    [Fact]
    public void Select_ReturnsAllWhenFewerThanK()
    {
        var candidates = new List<RetrievalCandidate>
        {
            Build("a-doc", 0),
            Build("b-doc", 1)
        };

        var selector = new ContextSelector();

        var selected = selector.Select(candidates, 5);

        Assert.Equal(2, selected.Count);
        Assert.Equal("a-doc", selected[0].DocumentId);
        Assert.Equal("b-doc", selected[1].DocumentId);
    }

    private static RetrievalCandidate Build(string documentId, int chunkIndex)
    {
        return new RetrievalCandidate(
            "q1",
            0,
            0.50f,
            0.50f,
            documentId,
            $"{documentId}_{chunkIndex}",
            chunkIndex,
            "text");
    }
}
