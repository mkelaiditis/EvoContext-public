namespace EvoContext.Core.Retrieval;

public interface ICandidateScorer
{
    IReadOnlyList<ScoredCandidate> Score(IReadOnlyList<RetrievalCandidate> candidates);
}
