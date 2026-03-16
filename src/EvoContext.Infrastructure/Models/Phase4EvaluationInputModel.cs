using System.Text.Json.Serialization;

namespace EvoContext.Infrastructure.Models;

public sealed record Phase4EvaluationInputModel(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("scenario_id")] string ScenarioId,
    [property: JsonPropertyName("answer_text")] string AnswerText,
    [property: JsonPropertyName("selected_chunks")] IReadOnlyList<Phase4SelectedChunkModel> SelectedChunks);

public sealed record Phase4SelectedChunkModel(
    [property: JsonPropertyName("document_id")] string DocumentId,
    [property: JsonPropertyName("chunk_id")] string ChunkId,
    [property: JsonPropertyName("chunk_index")] int ChunkIndex,
    [property: JsonPropertyName("chunk_text")] string ChunkText);
