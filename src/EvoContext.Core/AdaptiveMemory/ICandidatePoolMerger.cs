using EvoContext.Core.Retrieval;

namespace EvoContext.Core.AdaptiveMemory;

public interface ICandidatePoolMerger
{
    IReadOnlyList<RetrievalCandidate> Merge(IReadOnlyList<RetrievalCandidate> candidates);
}
