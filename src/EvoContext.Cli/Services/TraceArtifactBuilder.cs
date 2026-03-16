using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Runs;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Cli.Services;

public static class TraceArtifactBuilder
{
    public static TraceArtifact BuildTraceArtifact(
        ScenarioDefinition scenario,
        string queryText,
        Run5ExecutionRun run)
    {
        var finalResult = run.Run2Result ?? run.Run1Result;
        var finalEvaluation = run.Run2Evaluation ?? run.Run1Evaluation;
        var retrievalQueries = run.Run2QuerySet?.AllQueries ?? new[] { queryText };
        var runMode = run.Run2Result is null ? "run1" : "run2";

        var selectedChunks = finalResult.RetrievalSummary.SelectedChunks
            .Select(chunk => new TraceArtifactSelectedChunk(
                chunk.DocumentId,
                chunk.ChunkId,
                chunk.ChunkIndex,
                chunk.ChunkText ?? string.Empty))
            .ToList();

        var memoryUpdates = BuildMemoryUpdates(run);
        var scenarioResultPayload = BuildScenarioResultPayload(finalEvaluation);
        var detectedEvidenceItems = (finalResult.DetectedEvidenceItems ?? Array.Empty<EvoContext.Core.Evidence.DetectedEvidenceItem>())
            .Select(item => new TraceArtifactDetectedEvidenceItem(
                item.FactLabel,
                item.DocumentId,
                item.MatchedAnchor,
                item.ExtractedSnippet))
            .ToList();

        return new TraceArtifact(
            finalResult.RunId,
            scenario.ScenarioId,
            scenario.ScenarioId,
            queryText,
            runMode,
            DateTimeOffset.UtcNow.ToString("O"),
            retrievalQueries,
            finalResult.RetrievalSummary.RetrievedCandidates.Count,
            selectedChunks,
            finalResult.RetrievalSummary.ContextPack.CharCount,
            finalResult.Answer ?? string.Empty,
            finalEvaluation.ScoreTotal,
            finalEvaluation.QuerySuggestions,
            run.Run1Evaluation.ScoreTotal,
            run.Run2Evaluation?.ScoreTotal,
            run.Run2Evaluation is null ? null : run.ScoreDelta.ScoreDelta,
            memoryUpdates,
                scenarioResultPayload,
                detectedEvidenceItems,
                finalResult.EvidenceBlock ?? string.Empty);
    }

    public static RunVerificationEvidence BuildRunVerificationEvidence(
        ScenarioDefinition scenario,
        string queryText,
        Run5ExecutionRun run)
    {
        var finalRunId = run.Run2Result?.RunId ?? run.Run1Result.RunId;
        var run1SelectedChunks = run.Run1Result.RetrievalSummary.SelectedChunks
            .Select(chunk => new RunVerificationEvidenceSelectedChunk(
                chunk.DocumentId,
                chunk.ChunkId,
                chunk.ChunkIndex,
                chunk.ChunkText ?? string.Empty))
            .ToList();

        return new RunVerificationEvidence(
            finalRunId,
            scenario.ScenarioId,
            queryText,
            new RunVerificationEvidenceRun(
                run.Run1Result.Answer ?? string.Empty,
                run1SelectedChunks));
    }

    public static IReadOnlyList<string> BuildMemoryUpdates(Run5ExecutionRun run)
    {
        if (run.Run2Result is null || run.MemoryUpdates <= 0)
        {
            return Array.Empty<string>();
        }

        return run.Run2Result.RetrievalSummary.SelectedChunks
            .OrderBy(chunk => chunk.DocumentId, StringComparer.Ordinal)
            .ThenBy(chunk => chunk.ChunkIndex)
            .Select(chunk => chunk.ChunkId)
            .ToList();
    }

    public static object BuildScenarioResultPayload(EvaluationResult evaluation)
    {
        return evaluation.ScenarioResult switch
        {
            PolicyRefundScenarioResult policyResult => new PolicyRefundScenarioResultPayload(
                policyResult.PresentFactLabels,
                policyResult.MissingFactLabels,
                policyResult.HallucinationFlags,
                new PolicyRefundScoreBreakdownPayload(
                    policyResult.ScoreBreakdown.CompletenessPoints,
                    policyResult.ScoreBreakdown.FormatPoints,
                    policyResult.ScoreBreakdown.HallucinationPenalty,
                    policyResult.ScoreBreakdown.AccuracyCapApplied)),
            // Phase 7 contract mapping for runbook_502_v1 scenario_result payload.
            Runbook502ScenarioResult runbookResult => new Runbook502ScenarioResultPayload(
                runbookResult.PresentStepLabels,
                runbookResult.MissingStepLabels,
                runbookResult.OrderViolationLabels,
                new Runbook502ScoreBreakdownPayload(
                    runbookResult.ScoreBreakdown.StepCoveragePoints,
                    runbookResult.ScoreBreakdown.OrderCorrectPoints,
                    runbookResult.ScoreBreakdown.HallucinationPenalty)),
            _ => new Dictionary<string, object?>()
        };
    }

    public static IReadOnlyList<string> BuildChunkList(RunResult result)
    {
        return result.RetrievalSummary.SelectedChunks
            .Select(chunk => $"{chunk.DocumentId}:{chunk.ChunkId}:{chunk.ChunkIndex}")
            .ToList()
            .AsReadOnly();
    }
}
