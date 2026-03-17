using EvoContext.Core.Context;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;

namespace EvoContext.Core.Tests.Execution;

public static class RetrievalRenderingFixtures
{
    public static RetrievalCandidate CreateCandidate(
        int rank,
        string documentId,
        string chunkId,
        int chunkIndex,
        float similarity,
        string chunkText = "chunk text",
        string queryIdentifier = "q0",
        string? documentTitle = null,
        string? section = null,
        string? queryText = null)
    {
        return new RetrievalCandidate(
            queryIdentifier,
            rank,
            similarity,
            similarity,
            documentId,
            chunkId,
            chunkIndex,
            chunkText,
            documentTitle,
            section,
            queryText);
    }

    public static RunResult CreateRunResult(
        IReadOnlyList<RetrievalCandidate> retrievedCandidates,
        IReadOnlyList<RetrievalCandidate>? selectedChunks = null,
        string? answer = null,
        string scenarioId = "phase12_demo",
        string queryText = "Explain the policy")
    {
        var selected = selectedChunks ?? retrievedCandidates;
        var summary = new RetrievalSummary(
            retrievedCandidates,
            selected,
            new ContextPack("context", 120, selected.Count, 2200));

        return new RunResult(
            "phase12_run",
            new RunRequest(scenarioId, queryText, RunMode.Run1SimilarityOnly),
            summary,
            answer,
            EvaluationResult: null);
    }
}