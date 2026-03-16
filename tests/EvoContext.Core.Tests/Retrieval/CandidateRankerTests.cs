using EvoContext.Core.Retrieval;
using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests;

public sealed class CandidateRankerTests
{
    [Fact]
    public void Rank_ReturnsEmptyWhenNoCandidates()
    {
        var ranker = new CandidateRanker();

        var ordered = ranker.Rank(Array.Empty<ScoredCandidate>());

        Assert.Empty(ordered);
    }

    [Fact]
    public void Rank_ReturnsSingleCandidateUnchanged()
    {
        var candidate = Build("a-doc", 1, 0.12f);
        var ranker = new CandidateRanker();

        var ordered = ranker.Rank(new[] { candidate });

        var single = Assert.Single(ordered);
        Assert.Same(candidate, single);
    }

    [Fact]
    public void Rank_WhenScoresEqual_OrdersByDocumentIdThenChunkIndex()
    {
        var candidates = new List<ScoredCandidate>
        {
            Build("b-doc", 0, 0.50f),
            Build("a-doc", 2, 0.50f),
            Build("a-doc", 1, 0.50f)
        };

        var ranker = new CandidateRanker();

        var ordered = ranker.Rank(candidates);

        Assert.Collection(
            ordered,
            first => Assert.Equal("a-doc", first.Candidate.DocumentId),
            second => Assert.Equal("a-doc", second.Candidate.DocumentId),
            third => Assert.Equal("b-doc", third.Candidate.DocumentId));
        Assert.Equal(1, ordered[0].Candidate.ChunkIndex);
        Assert.Equal(2, ordered[1].Candidate.ChunkIndex);
    }

    [Fact]
    public void Rank_OrdersByCombinedScoreThenDocumentIdThenChunkIndex()
    {
        var candidates = new List<ScoredCandidate>
        {
            Build("b-doc", 2, 0.50f),
            Build("a-doc", 3, 0.50f),
            Build("a-doc", 1, 0.50f),
            Build("a-doc", 0, 0.20f),
            Build("c-doc", 5, 0.90f)
        };

        var ranker = new CandidateRanker();

        var ordered = ranker.Rank(candidates);

        Assert.Collection(
            ordered,
            first => Assert.Equal("c-doc", first.Candidate.DocumentId),
            second =>
            {
                Assert.Equal("a-doc", second.Candidate.DocumentId);
                Assert.Equal(1, second.Candidate.ChunkIndex);
            },
            third =>
            {
                Assert.Equal("a-doc", third.Candidate.DocumentId);
                Assert.Equal(3, third.Candidate.ChunkIndex);
            },
            fourth =>
            {
                Assert.Equal("b-doc", fourth.Candidate.DocumentId);
                Assert.Equal(2, fourth.Candidate.ChunkIndex);
            },
            fifth =>
            {
                Assert.Equal("a-doc", fifth.Candidate.DocumentId);
                Assert.Equal(0, fifth.Candidate.ChunkIndex);
            });
    }

    private static ScoredCandidate Build(string documentId, int chunkIndex, float combinedScore)
    {
        var candidate = new RetrievalCandidate(
            "q1",
            0,
            combinedScore,
            combinedScore,
            documentId,
            $"{documentId}_{chunkIndex}",
            chunkIndex,
            "text");
        var score = new CandidateScore(combinedScore, 0f, 0f, combinedScore);
        return new ScoredCandidate(candidate, score);
    }
}
