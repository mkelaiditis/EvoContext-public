using EvoContext.Core.Retrieval;

namespace EvoContext.Core.AdaptiveMemory;

public interface IRun2CandidateScorer
{
    IReadOnlyList<ScoredCandidate> Score(
        IReadOnlyList<RetrievalCandidate> candidates,
        UsefulnessMemorySnapshot? snapshot);
}
