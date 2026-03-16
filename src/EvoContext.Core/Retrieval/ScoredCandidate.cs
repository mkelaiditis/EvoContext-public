namespace EvoContext.Core.Retrieval;

public sealed record ScoredCandidate(
    RetrievalCandidate Candidate,
    CandidateScore Score);
