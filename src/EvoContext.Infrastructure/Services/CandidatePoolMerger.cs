using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Retrieval;

namespace EvoContext.Infrastructure.Services;

public sealed class CandidatePoolMerger : ICandidatePoolMerger
{
    public IReadOnlyList<RetrievalCandidate> Merge(IReadOnlyList<RetrievalCandidate> candidates)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        if (candidates.Count == 0)
        {
            return Array.Empty<RetrievalCandidate>();
        }

        var bestByChunk = new Dictionary<string, RetrievalCandidate>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (!bestByChunk.TryGetValue(candidate.ChunkId, out var existing))
            {
                bestByChunk[candidate.ChunkId] = candidate;
                continue;
            }

            if (candidate.SimilarityScore > existing.SimilarityScore)
            {
                bestByChunk[candidate.ChunkId] = candidate;
            }
        }

        return bestByChunk.Values
            .OrderBy(candidate => candidate.DocumentId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.ChunkIndex)
            .ToList();
    }
}
