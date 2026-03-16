using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EvoContext.Core.Documents;
using EvoContext.Core.Embeddings;
using EvoContext.Core.Text;
using EvoContext.Core.VectorStore;
using EvoContext.Infrastructure.Models;
using Grpc.Core;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace EvoContext.Infrastructure.Services;

public sealed class QdrantIndexService : IVectorIndex
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;

    public QdrantIndexService(string host, int port, bool https, string? apiKey, string collectionName)
    {
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

        _client = new QdrantClient(host, port, https, apiKey);
        _collectionName = collectionName;
    }

    public async Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken = default)
    {
        if (vectorSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vectorSize), "Vector size must be positive.");
        }

        var vectorParams = new VectorParams
        {
            Size = (uint)vectorSize,
            Distance = Distance.Cosine
        };

        try
        {
            await _client.CreateCollectionAsync(_collectionName, vectorParams, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.AlreadyExists)
        {
            // Collection already exists; no action needed.
        }
    }

    public Task UpsertAsync(
        IReadOnlyList<EmbeddingVector> vectors,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        if (chunks is null)
        {
            throw new ArgumentNullException(nameof(chunks));
        }

        if (vectors is null)
        {
            throw new ArgumentNullException(nameof(vectors));
        }

        if (chunks.Count != vectors.Count)
        {
            throw new InvalidOperationException("Chunk count does not match vector count.");
        }

        if (vectors.Count == 0)
        {
            return Task.CompletedTask;
        }

        var vectorSize = vectors[0].Values.Count;
        if (vectorSize <= 0)
        {
            throw new InvalidOperationException("Vector size must be positive.");
        }

        var embeddings = vectors.Select(vector => new ReadOnlyMemory<float>(vector.Values.ToArray())).ToList();
        return UpsertCore(chunks, embeddings, cancellationToken);
    }

    public Task RecreateCollectionAsync(int vectorSize, CancellationToken cancellationToken = default)
    {
        if (vectorSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vectorSize), "Vector size must be positive.");
        }

        var vectorParams = new VectorParams
        {
            Size = (uint)vectorSize,
            Distance = Distance.Cosine
        };

        return _client.RecreateCollectionAsync(_collectionName, vectorParams, cancellationToken: cancellationToken);
    }

    public Task<ulong> CountAsync(CancellationToken cancellationToken = default)
    {
        return _client.CountAsync(_collectionName, cancellationToken: cancellationToken);
    }

    private Task<UpdateResult> UpsertCore(
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> vectors,
        CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
        {
            return Task.FromResult(new UpdateResult());
        }

        var points = new List<PointStruct>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var vector = vectors[i];
            var pointId = CreatePointId(chunk.ChunkId);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = pointId.ToString("D") },
                Vectors = vector.ToArray()
            };

            point.Payload[QdrantPayloadKeys.DocumentId] = chunk.DocumentId;
            point.Payload[QdrantPayloadKeys.ChunkId] = chunk.ChunkId;
            point.Payload[QdrantPayloadKeys.ChunkIndex] = chunk.ChunkIndex;
            point.Payload[QdrantPayloadKeys.Text] = chunk.Text;

            if (chunk.DocumentTitle.HasValue())
            {
                point.Payload[QdrantPayloadKeys.DocumentTitle] = chunk.DocumentTitle!;
            }

            if (chunk.Section.HasValue())
            {
                point.Payload[QdrantPayloadKeys.Section] = chunk.Section!;
            }

            points.Add(point);
        }

        return _client.UpsertAsync(_collectionName, points, cancellationToken: cancellationToken);
    }

    private static Guid CreatePointId(string chunkId)
    {
        if (string.IsNullOrWhiteSpace(chunkId))
        {
            throw new InvalidOperationException("ChunkId is required for point ID generation.");
        }

        var bytes = Encoding.UTF8.GetBytes(chunkId);
        var hash = SHA256.HashData(bytes);
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}
