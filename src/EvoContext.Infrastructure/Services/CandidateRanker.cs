using EvoContext.Core.Retrieval;

namespace EvoContext.Infrastructure.Services;

public sealed class CandidateRanker : ICandidateRanker
{
    public IReadOnlyList<ScoredCandidate> Rank(IReadOnlyList<ScoredCandidate> candidates)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score.CombinedScore)
            .ThenBy(candidate => candidate.Candidate.DocumentId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Candidate.ChunkIndex)
            .ToList();
    }
}
