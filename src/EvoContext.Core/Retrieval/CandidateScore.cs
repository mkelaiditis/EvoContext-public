namespace EvoContext.Core.Retrieval;

public sealed record CandidateScore(
    float SimilarityScore,
    float RecencyScore,
    float UsefulnessScore,
    float CombinedScore);
