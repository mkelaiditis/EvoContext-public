using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Retrieval;

namespace EvoContext.Infrastructure.Services;

public sealed class Run2CandidateScorer : IRun2CandidateScorer
{
    public IReadOnlyList<ScoredCandidate> Score(
        IReadOnlyList<RetrievalCandidate> candidates,
        UsefulnessMemorySnapshot? snapshot)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        var weights = BuildWeights(snapshot);
        var scored = new List<ScoredCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var usefulnessWeight = weights.TryGetValue(candidate.ChunkId, out var weight) ? weight : 0f;
            var similarityScore = candidate.SimilarityScore;
            var combinedScore = similarityScore + usefulnessWeight;
            // Phase 5 keeps recency scoring disabled; keep the recency slot at 0.
            var score = new CandidateScore(similarityScore, 0f, usefulnessWeight, combinedScore);
            scored.Add(new ScoredCandidate(candidate, score));
        }

        return scored;
    }

    private static Dictionary<string, float> BuildWeights(UsefulnessMemorySnapshot? snapshot)
    {
        if (snapshot?.Items is null || snapshot.Items.Count == 0)
        {
            return new Dictionary<string, float>(StringComparer.Ordinal);
        }

        var weights = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ChunkId))
            {
                continue;
            }

            weights[item.ChunkId] = item.UsefulnessScore;
        }

        return weights;
    }
}
