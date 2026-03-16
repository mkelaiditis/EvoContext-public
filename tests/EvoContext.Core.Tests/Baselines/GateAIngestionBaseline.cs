using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EvoContext.Core.Tests.Baselines;

internal static class GateAIngestionBaseline
{
    private const string BaselineFileName = "gate-a-ingestion.json";

    public static string BaselinePath => Path.Combine(
        TestDatasetPaths.RepoRoot,
        "tests",
        "EvoContext.Core.Tests",
        "Baselines",
        BaselineFileName);

    public static BaselineSnapshot Load()
    {
        var json = File.ReadAllText(BaselinePath);
        var snapshot = JsonSerializer.Deserialize<BaselineSnapshot>(json, JsonOptions());

        if (snapshot is null)
        {
            throw new InvalidOperationException("Gate A ingestion baseline could not be loaded.");
        }

        return snapshot;
    }

    public static IReadOnlyList<BaselineChunk> LoadChunks()
    {
        return Load().Chunks;
    }

    public static IReadOnlyList<BaselineDocument> LoadDocuments()
    {
        return Load().Documents;
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
}

internal sealed record BaselineSnapshot(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("dataset")] string Dataset,
    [property: JsonPropertyName("chunking")] BaselineChunking Chunking,
    [property: JsonPropertyName("documents")] IReadOnlyList<BaselineDocument> Documents,
    [property: JsonPropertyName("chunks")] IReadOnlyList<BaselineChunk> Chunks);

internal sealed record BaselineChunking(
    [property: JsonPropertyName("chunk_size")] int ChunkSize,
    [property: JsonPropertyName("chunk_overlap")] int ChunkOverlap);

internal sealed record BaselineDocument(
    [property: JsonPropertyName("doc_id")] string DocId,
    [property: JsonPropertyName("char_length")] int CharLength,
    [property: JsonPropertyName("chunk_count")] int ChunkCount);

internal sealed record BaselineChunk(
    [property: JsonPropertyName("doc_id")] string DocId,
    [property: JsonPropertyName("chunk_index")] int ChunkIndex,
    [property: JsonPropertyName("chunk_id")] string ChunkId,
    [property: JsonPropertyName("start_char")] int StartChar,
    [property: JsonPropertyName("end_char")] int EndChar,
    [property: JsonPropertyName("text")] string Text);
