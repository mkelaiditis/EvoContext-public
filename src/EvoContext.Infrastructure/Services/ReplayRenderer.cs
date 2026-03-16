using System.Text.Json;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Infrastructure.Services;

public sealed class ReplayRenderer
{
    public void Render(IRunRenderer renderer, TraceArtifact artifact)
    {
        if (renderer is null)
        {
            throw new ArgumentNullException(nameof(renderer));
        }

        if (artifact is null)
        {
            throw new ArgumentNullException(nameof(artifact));
        }

        var timestamp = ResolveTimestamp(artifact.TimestampUtc);
        var selectedChunks = artifact.SelectedChunks
            .Select(chunk => new Dictionary<string, object?>
            {
                ["document_id"] = chunk.DocumentId,
                ["chunk_id"] = chunk.ChunkId,
                ["chunk_index"] = chunk.ChunkIndex
            })
            .ToList();

        renderer.OnEvent(new TraceEvent(
            TraceEventType.RetrievalCompleted,
            artifact.RunId,
            artifact.ScenarioId,
            1,
            new Dictionary<string, object?>
            {
                ["retrieval_query_count"] = artifact.RetrievalQueries.Count,
                ["retrieved_count"] = artifact.CandidatePoolSize,
                ["query_text"] = artifact.Query,
                ["retrieval_queries"] = artifact.RetrievalQueries,
                ["timestamp_utc"] = timestamp
            },
            timestamp));

        renderer.OnEvent(new TraceEvent(
            TraceEventType.ContextSelected,
            artifact.RunId,
            artifact.ScenarioId,
            2,
            new Dictionary<string, object?>
            {
                ["selected_count"] = selectedChunks.Count,
                ["selected"] = selectedChunks,
                ["context_character_count"] = artifact.ContextSizeChars,
                ["timestamp_utc"] = timestamp
            },
            timestamp));

        renderer.OnEvent(new TraceEvent(
            TraceEventType.GenerationCompleted,
            artifact.RunId,
            artifact.ScenarioId,
            3,
            new Dictionary<string, object?>
            {
                ["raw_model_output"] = artifact.Answer,
                ["timestamp_utc"] = timestamp
            },
            timestamp));

        var evaluationMetadata = BuildEvaluationMetadata(artifact, timestamp);
        renderer.OnEvent(new TraceEvent(
            TraceEventType.EvaluationCompleted,
            artifact.RunId,
            artifact.ScenarioId,
            4,
            evaluationMetadata,
            timestamp));

        if (string.Equals(artifact.RunMode, "run2", StringComparison.OrdinalIgnoreCase))
        {
            renderer.OnEvent(new TraceEvent(
                TraceEventType.Run2Triggered,
                artifact.RunId,
                artifact.ScenarioId,
                5,
                new Dictionary<string, object?>
                {
                    ["label"] = "Run 2 triggered — score below threshold",
                    ["expanded_query_count"] = artifact.RetrievalQueries.Count,
                    ["timestamp_utc"] = timestamp
                },
                timestamp));
        }

        renderer.OnEvent(new TraceEvent(
            TraceEventType.RunFinished,
            artifact.RunId,
            artifact.ScenarioId,
            6,
            new Dictionary<string, object?>
            {
                ["run_id"] = artifact.RunId,
                ["scenario_id"] = artifact.ScenarioId,
                ["query_text"] = artifact.Query,
                ["run_mode"] = artifact.RunMode,
                ["score_run1"] = artifact.ScoreRun1,
                ["score_run2"] = artifact.ScoreRun2,
                ["score_delta"] = artifact.ScoreDelta,
                ["memory_updates_count"] = artifact.MemoryUpdates.Count,
                ["timestamp_utc"] = timestamp
            },
            timestamp));

        renderer.OnRunComplete(BuildRunSummary(artifact));
    }

    private static IReadOnlyDictionary<string, object?> BuildEvaluationMetadata(
        TraceArtifact artifact,
        DateTimeOffset timestamp)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["score_total"] = artifact.ScoreTotal,
            ["query_suggestions"] = artifact.QuerySuggestions,
            ["timestamp_utc"] = timestamp
        };

        var missingItems = new List<string>();

        switch (artifact.ScenarioResult)
        {
            case PolicyRefundScenarioResultPayload policyPayload:
                metadata["present_fact_labels"] = policyPayload.PresentFactLabels;
                metadata["missing_fact_labels"] = policyPayload.MissingFactLabels;
                metadata["hallucination_flags"] = policyPayload.HallucinationFlags;
                missingItems.AddRange(policyPayload.MissingFactLabels);
                break;
            case Runbook502ScenarioResultPayload runbookPayload:
                metadata["present_step_labels"] = runbookPayload.PresentStepLabels;
                metadata["missing_step_labels"] = runbookPayload.MissingStepLabels;
                metadata["order_violation_labels"] = runbookPayload.OrderViolationLabels;
                missingItems.AddRange(runbookPayload.MissingStepLabels);
                break;
            case JsonElement scenarioResultElement when scenarioResultElement.ValueKind == JsonValueKind.Object:
                AddJsonArrayProperty(scenarioResultElement, "present_fact_labels", metadata);
                AddJsonArrayProperty(scenarioResultElement, "present_step_labels", metadata);
                AddJsonArrayProperty(scenarioResultElement, "missing_fact_labels", metadata, missingItems);
                AddJsonArrayProperty(scenarioResultElement, "missing_step_labels", metadata, missingItems);
                AddJsonArrayProperty(scenarioResultElement, "hallucination_flags", metadata);
                AddJsonArrayProperty(scenarioResultElement, "order_violation_labels", metadata);
                break;
        }

        if (missingItems.Count > 0)
        {
            metadata["missing_items"] = missingItems;
        }

        return metadata;
    }

    private static void AddJsonArrayProperty(
        JsonElement source,
        string propertyName,
        IDictionary<string, object?> target,
        List<string>? missingItems = null)
    {
        if (!source.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var values = property.EnumerateArray()
            .Select(item => item.ToString())
            .ToList();

        target[propertyName] = values;
        missingItems?.AddRange(values);
    }

    private static RunSummary BuildRunSummary(TraceArtifact artifact)
    {
        var runMode = string.Equals(artifact.RunMode, "run2", StringComparison.OrdinalIgnoreCase)
            ? RunMode.Run2FeedbackExpanded
            : RunMode.Run1AnswerGeneration;

        return new RunSummary(
            artifact.RunId,
            artifact.ScenarioId,
            runMode,
            artifact.ScoreRun1,
            artifact.ScoreRun2,
            artifact.ScoreDelta,
            artifact.MemoryUpdates.Count);
    }

    private static DateTimeOffset ResolveTimestamp(string? timestampUtc)
    {
        if (DateTimeOffset.TryParse(timestampUtc, out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.UtcNow;
    }
}
