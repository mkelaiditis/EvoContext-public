namespace EvoContext.Core.Retrieval;

public interface ICandidateRanker
{
    IReadOnlyList<ScoredCandidate> Rank(IReadOnlyList<ScoredCandidate> candidates);
}
