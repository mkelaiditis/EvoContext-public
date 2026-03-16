using EvoContext.Core.Retrieval;

namespace EvoContext.Infrastructure.Services;

public sealed class CandidateScorer : ICandidateScorer
{
    public IReadOnlyList<ScoredCandidate> Score(IReadOnlyList<RetrievalCandidate> candidates)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        var scored = new List<ScoredCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var similarityScore = candidate.RawSimilarityScore;
            var score = new CandidateScore(similarityScore, 0f, 0f, similarityScore);
            scored.Add(new ScoredCandidate(candidate, score));
        }

        return scored;
    }
}
