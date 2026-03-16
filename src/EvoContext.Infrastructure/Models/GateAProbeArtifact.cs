using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EvoContext.Infrastructure.Models;

public sealed record GateAProbeArtifact(
    [property: JsonPropertyName("query_text")] string QueryText,
    [property: JsonPropertyName("timestamp_utc")] string TimestampUtc,
    [property: JsonPropertyName("embedding_model")] string EmbeddingModel,
    [property: JsonPropertyName("collection_name")] string CollectionName,
    [property: JsonPropertyName("top10_results")] IReadOnlyList<GateAProbeResult> Top10Results,
    [property: JsonPropertyName("doc6_in_top3")] bool Doc6InTop3);

public sealed record GateAProbeResult(
    [property: JsonPropertyName("doc_id")] string DocId,
    [property: JsonPropertyName("chunk_id")] string ChunkId,
    [property: JsonPropertyName("chunk_index")] int ChunkIndex,
    [property: JsonPropertyName("similarity_score")] float SimilarityScore);
