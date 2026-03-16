using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Config;
using EvoContext.Core.Logging;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Text;
using EvoContext.Infrastructure.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Serilog;

namespace EvoContext.Infrastructure.Services;

public sealed class RetrievalService : IRetriever
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;
    private readonly EmbeddingService _embedder;
    private readonly ILogger _logger;

    public RetrievalService(
        string host,
        int port,
        bool https,
        string? apiKey,
        string collectionName,
        CoreConfigSnapshot config,
        EmbeddingService? embedder = null,
        ILogger? logger = null)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Qdrant host is required.", nameof(host));
        }

        if (port <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Qdrant port must be positive.");
        }

        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentException("Collection name is required.", nameof(collectionName));
        }

        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<RetrievalService>();
        _client = new QdrantClient(host, port, https, apiKey);
        _collectionName = collectionName;
        _embedder = embedder ?? new EmbeddingService(config, apiKey, _logger.ForContext<EmbeddingService>());
    }

    public async Task<IReadOnlyList<RetrievalResult>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        }

        var points = (await _client.SearchAsync(
                _collectionName,
                queryVector,
                limit: (ulong)limit,
                payloadSelector: new WithPayloadSelector { Enable = true },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false))
            .ToList();

        var results = new List<RetrievalResult>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            results.Add(new RetrievalResult(
                GetDocId(point),
                GetChunkIndex(point),
                point.Score,
                i + 1));
        }

        _logger
            .WithProperties(
                ("collection_name", _collectionName),
                ("limit", limit),
                ("result_count", results.Count),
                ("top_score", results.Count > 0 ? results[0].Score : null))
            .Debug("Vector search completed");

        return results;
    }

    public async Task<IReadOnlyList<RetrievalCandidate>> RetrieveAsync(
        RetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.RetrievalN <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.RetrievalN), "Retrieval N must be positive.");
        }

        if (string.IsNullOrWhiteSpace(request.QueryIdentifier))
        {
            throw new ArgumentException("Query identifier is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.QueryText))
        {
            throw new ArgumentException("Query text is required.", nameof(request));
        }

        var embedding = await _embedder.EmbedAsync(request.QueryText, cancellationToken).ConfigureAwait(false);
        var queryVector = new ReadOnlyMemory<float>(embedding.Values.ToArray());

        var points = (await _client.SearchAsync(
                _collectionName,
                queryVector,
                limit: (ulong)request.RetrievalN,
                payloadSelector: new WithPayloadSelector { Enable = true },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false))
            .ToList();

        var results = new List<RetrievalCandidate>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var docId = GetDocId(point);
            var chunkIndex = GetChunkIndex(point);
            var chunkId = GetChunkIdString(point);
            var chunkText = GetChunkText(point);
            var documentTitle = GetOptionalPayloadString(point, QdrantPayloadKeys.DocumentTitle);
            var section = GetOptionalPayloadString(point, QdrantPayloadKeys.Section);

            results.Add(new RetrievalCandidate(
                request.QueryIdentifier,
                i + 1,
                point.Score,
                point.Score,
                docId,
                chunkId,
                chunkIndex,
                chunkText,
                documentTitle,
                section,
                request.QueryText));
        }

        _logger
            .WithProperties(
                ("collection_name", _collectionName),
                ("query_identifier", request.QueryIdentifier),
                ("query_text", request.QueryText),
                ("retrieved_count", results.Count),
                ("top_score", results.Count > 0 ? results[0].SimilarityScore : null),
                ("top_chunk_id", results.Count > 0 ? results[0].ChunkId : null))
            .Debug("Retrieval search completed");

        return results;
    }

    private static string GetDocId(ScoredPoint point)
    {
        if (point.Payload is null || !point.Payload.TryGetValue(QdrantPayloadKeys.DocumentId, out var value))
        {
            throw new InvalidOperationException("Qdrant payload missing doc_id.");
        }

        return value.StringValue ?? throw new InvalidOperationException("Qdrant payload doc_id is null.");
    }

    private static string GetChunkIdString(ScoredPoint point)
    {
        if (point.Payload is null || !point.Payload.TryGetValue(QdrantPayloadKeys.ChunkId, out var value))
        {
            return BuildFallbackChunkId(point);
        }

        if (value.KindCase == Value.KindOneofCase.StringValue && !string.IsNullOrWhiteSpace(value.StringValue))
        {
            return value.StringValue;
        }

        if (value.KindCase == Value.KindOneofCase.IntegerValue)
        {
            return value.IntegerValue.ToString();
        }

        if (value.KindCase == Value.KindOneofCase.DoubleValue)
        {
            return ((int)value.DoubleValue).ToString();
        }

        return BuildFallbackChunkId(point);
    }

    private static int GetChunkIndex(ScoredPoint point)
    {
        if (point.Payload is null || !point.Payload.TryGetValue(QdrantPayloadKeys.ChunkIndex, out var value))
        {
            throw new InvalidOperationException("Qdrant payload missing chunk_index.");
        }

        if (value.KindCase == Value.KindOneofCase.IntegerValue)
        {
            return (int)value.IntegerValue;
        }

        if (value.KindCase == Value.KindOneofCase.DoubleValue)
        {
            return (int)value.DoubleValue;
        }

        if (value.KindCase == Value.KindOneofCase.StringValue
            && int.TryParse(value.StringValue, out var parsedChunkIndex))
        {
            return parsedChunkIndex;
        }

        throw new InvalidOperationException("Qdrant payload chunk_index is not numeric.");
    }

    private static string GetChunkText(ScoredPoint point)
    {
        if (point.Payload is null)
        {
            throw new InvalidOperationException("Qdrant payload is missing.");
        }

        if (point.Payload.TryGetValue(QdrantPayloadKeys.Text, out var value))
        {
            return value.StringValue ?? string.Empty;
        }

        if (point.Payload.TryGetValue(QdrantPayloadKeys.LegacyChunkText, out value))
        {
            return value.StringValue ?? string.Empty;
        }

        throw new InvalidOperationException("Qdrant payload missing text fields.");
    }

    private static string BuildChunkId(string documentId, int chunkIndex)
    {
        return $"{documentId}_{chunkIndex}";
    }

    private static string BuildFallbackChunkId(ScoredPoint point)
    {
        var documentId = GetDocId(point);
        var chunkIndex = GetChunkIndex(point);
        return BuildChunkId(documentId, chunkIndex);
    }

    private static string? GetOptionalPayloadString(ScoredPoint point, string key)
    {
        if (point.Payload is null || !point.Payload.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.KindCase switch
        {
            Value.KindOneofCase.StringValue => value.StringValue.NormalizeOptional(),
            Value.KindOneofCase.IntegerValue => value.IntegerValue.ToString(CultureInfo.InvariantCulture).NormalizeOptional(),
            Value.KindOneofCase.DoubleValue => value.DoubleValue.ToString(CultureInfo.InvariantCulture).NormalizeOptional(),
            _ => null
        };
    }
}
