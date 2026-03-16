using System.Text.Json.Serialization;

namespace EvoContext.Infrastructure.Models;

public sealed record TraceArtifact(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("scenario_id")] string ScenarioId,
    [property: JsonPropertyName("dataset_id")] string DatasetId,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("run_mode")] string RunMode,
    [property: JsonPropertyName("timestamp_utc")] string TimestampUtc,
    [property: JsonPropertyName("retrieval_queries")] IReadOnlyList<string> RetrievalQueries,
    [property: JsonPropertyName("candidate_pool_size")] int CandidatePoolSize,
    [property: JsonPropertyName("selected_chunks")] IReadOnlyList<TraceArtifactSelectedChunk> SelectedChunks,
    [property: JsonPropertyName("context_size_chars")] int ContextSizeChars,
    [property: JsonPropertyName("answer")] string Answer,
    [property: JsonPropertyName("score_total")] int ScoreTotal,
    [property: JsonPropertyName("query_suggestions")] IReadOnlyList<string> QuerySuggestions,
    [property: JsonPropertyName("score_run1")] int ScoreRun1,
    [property: JsonPropertyName("score_run2")] int? ScoreRun2,
    [property: JsonPropertyName("score_delta")] int? ScoreDelta,
    [property: JsonPropertyName("memory_updates")] IReadOnlyList<string> MemoryUpdates,
    [property: JsonPropertyName("scenario_result")] object ScenarioResult,
    [property: JsonPropertyName("detected_evidence_items")] IReadOnlyList<TraceArtifactDetectedEvidenceItem>? DetectedEvidenceItems = null,
    [property: JsonPropertyName("evidence_block")] string EvidenceBlock = "");

public sealed record TraceArtifactSelectedChunk(
    [property: JsonPropertyName("document_id")] string DocumentId,
    [property: JsonPropertyName("chunk_id")] string ChunkId,
    [property: JsonPropertyName("chunk_index")] int ChunkIndex,
    [property: JsonPropertyName("chunk_text")] string ChunkText);

public sealed record TraceArtifactDetectedEvidenceItem(
    [property: JsonPropertyName("fact_label")] string FactLabel,
    [property: JsonPropertyName("document_id")] string DocumentId,
    [property: JsonPropertyName("matched_anchor")] string MatchedAnchor,
    [property: JsonPropertyName("extracted_snippet")] string ExtractedSnippet);

public sealed record PolicyRefundScenarioResultPayload(
    [property: JsonPropertyName("present_fact_labels")] IReadOnlyList<string> PresentFactLabels,
    [property: JsonPropertyName("missing_fact_labels")] IReadOnlyList<string> MissingFactLabels,
    [property: JsonPropertyName("hallucination_flags")] IReadOnlyList<string> HallucinationFlags,
    [property: JsonPropertyName("score_breakdown")] PolicyRefundScoreBreakdownPayload ScoreBreakdown);

public sealed record PolicyRefundScoreBreakdownPayload(
    [property: JsonPropertyName("completeness_points")] int CompletenessPoints,
    [property: JsonPropertyName("format_points")] int FormatPoints,
    [property: JsonPropertyName("hallucination_penalty")] int HallucinationPenalty,
    [property: JsonPropertyName("accuracy_cap_applied")] bool AccuracyCapApplied);

public sealed record Runbook502ScenarioResultPayload(
    [property: JsonPropertyName("present_step_labels")] IReadOnlyList<string> PresentStepLabels,
    [property: JsonPropertyName("missing_step_labels")] IReadOnlyList<string> MissingStepLabels,
    [property: JsonPropertyName("order_violation_labels")] IReadOnlyList<string> OrderViolationLabels,
    [property: JsonPropertyName("score_breakdown")] Runbook502ScoreBreakdownPayload ScoreBreakdown);

public sealed record Runbook502ScoreBreakdownPayload(
    [property: JsonPropertyName("step_coverage_points")] int StepCoveragePoints,
    [property: JsonPropertyName("order_correct_points")] int OrderCorrectPoints,
    [property: JsonPropertyName("hallucination_penalty")] int HallucinationPenalty);
