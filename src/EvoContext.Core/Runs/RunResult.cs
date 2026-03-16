using EvoContext.Core.Evidence;
using EvoContext.Core.Context;
using EvoContext.Core.Retrieval;

namespace EvoContext.Core.Runs;

public sealed record RetrievalSummary(
    IReadOnlyList<RetrievalCandidate> RetrievedCandidates,
    IReadOnlyList<RetrievalCandidate> SelectedChunks,
    ContextPack ContextPack);

public sealed record RunResult(
    string RunId,
    RunRequest RunRequestSnapshot,
    RetrievalSummary RetrievalSummary,
    string? Answer,
    object? EvaluationResult,
    IReadOnlyList<DetectedEvidenceItem>? DetectedEvidenceItems = null,
    string? EvidenceBlock = null);
