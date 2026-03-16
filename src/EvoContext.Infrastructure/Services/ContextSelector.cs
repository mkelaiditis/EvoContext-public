using EvoContext.Core.Context;
using EvoContext.Core.Retrieval;

namespace EvoContext.Infrastructure.Services;

public sealed class ContextSelector : IContextSelector
{
    public IReadOnlyList<RetrievalCandidate> Select(IReadOnlyList<RetrievalCandidate> rankedCandidates, int selectionK)
    {
        if (rankedCandidates is null)
        {
            throw new ArgumentNullException(nameof(rankedCandidates));
        }

        if (selectionK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectionK), "Selection K must be positive.");
        }

        return rankedCandidates
            .Take(selectionK)
            .ToList();
    }
}
