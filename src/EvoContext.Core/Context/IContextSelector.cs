using EvoContext.Core.Retrieval;

namespace EvoContext.Core.Context;

public interface IContextSelector
{
    IReadOnlyList<RetrievalCandidate> Select(IReadOnlyList<RetrievalCandidate> rankedCandidates, int selectionK);
}
