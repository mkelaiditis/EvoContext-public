using System.Text.Json.Serialization;

namespace EvoContext.Infrastructure.Models;

public sealed record RunVerificationEvidence(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("scenario_id")] string ScenarioId,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("run1")] RunVerificationEvidenceRun Run1);

public sealed record RunVerificationEvidenceRun(
    [property: JsonPropertyName("answer")] string Answer,
    [property: JsonPropertyName("selected_chunks")] IReadOnlyList<RunVerificationEvidenceSelectedChunk> SelectedChunks);

public sealed record RunVerificationEvidenceSelectedChunk(
    [property: JsonPropertyName("document_id")] string DocumentId,
    [property: JsonPropertyName("chunk_id")] string ChunkId,
    [property: JsonPropertyName("chunk_index")] int ChunkIndex,
    [property: JsonPropertyName("chunk_text")] string ChunkText);