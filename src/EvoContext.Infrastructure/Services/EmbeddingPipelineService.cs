using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EvoContext.Core.Config;
using EvoContext.Core.Documents;
using EvoContext.Core.Logging;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Models;
using Serilog;

namespace EvoContext.Infrastructure.Services;

public sealed class EmbeddingPipelineService
{
    private const string ProbeQuestion = "What is the refund policy for annual subscriptions?";

    private readonly CoreConfigSnapshot _config;
    private readonly string _collectionName;
    private readonly IDatasetLoader _documentLoader;
    private readonly Chunker _chunker;
    private readonly EmbeddingService _embedder;
    private readonly QdrantIndexService _indexService;
    private readonly RetrievalService _retriever;
    private readonly IGateAProbeWriter _probeWriter;
    private readonly ITraceEmitter _traceEmitter;
    private readonly ILogger _logger;
    private readonly IStageProgressReporter? _stageProgressReporter;

    public EmbeddingPipelineService(
        CoreConfigSnapshot config,
        string collectionName,
        IDatasetLoader documentLoader,
        Chunker chunker,
        EmbeddingService embedder,
        QdrantIndexService indexService,
        RetrievalService retriever,
        IGateAProbeWriter probeWriter,
        ITraceEmitter traceEmitter,
        ILogger? logger = null,
        IStageProgressReporter? stageProgressReporter = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _collectionName = string.IsNullOrWhiteSpace(collectionName)
            ? throw new ArgumentException("Collection name is required.", nameof(collectionName))
            : collectionName;
        _documentLoader = documentLoader ?? throw new ArgumentNullException(nameof(documentLoader));
        _chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _indexService = indexService ?? throw new ArgumentNullException(nameof(indexService));
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _probeWriter = probeWriter ?? throw new ArgumentNullException(nameof(probeWriter));
        _traceEmitter = traceEmitter ?? throw new ArgumentNullException(nameof(traceEmitter));
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<EmbeddingPipelineService>();
        _stageProgressReporter = stageProgressReporter;
    }

    public async Task<EmbeddingPipelineResult> ExecuteAsync(
        string scenarioId,
        string datasetPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("Scenario id is required.", nameof(scenarioId));
        }

        if (string.IsNullOrWhiteSpace(datasetPath))
        {
            throw new ArgumentException("Dataset path is required.", nameof(datasetPath));
        }

        var documents = await _documentLoader.LoadAsync(datasetPath, cancellationToken).ConfigureAwait(false);
        var chunks = documents.SelectMany(document => _chunker.Chunk(document)).ToList();
        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("No chunks were generated from the dataset.");
        }

        _logger
            .WithProperties(
                ("scenario_id", scenarioId),
                ("dataset_path", datasetPath),
                ("document_count", documents.Count),
                ("chunk_count", chunks.Count))
            .Debug("Embedding pipeline prepared");

        var embeddings = await ExecuteStageAsync(
                "Generating embeddings for dataset...",
                showSpinner: true,
                innerCancellationToken => _embedder.EmbedBatchAsync(chunks.Select(chunk => chunk.Text).ToList(), innerCancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
        var vectorSize = embeddings[0].Values.Count;

        await _indexService.RecreateCollectionAsync(vectorSize, cancellationToken).ConfigureAwait(false);
        await _indexService.UpsertAsync(embeddings, chunks, cancellationToken).ConfigureAwait(false);

        _logger
            .WithProperties(
                ("scenario_id", scenarioId),
                ("collection_name", _collectionName),
                ("vector_dimension", vectorSize),
                ("chunk_count", chunks.Count))
            .Debug("Embedding index updated");

        var runId = BuildRunId("embed");
        await _traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.EmbeddingIngestCompleted,
            runId,
            scenarioId,
            1,
            new Dictionary<string, object?>
            {
                ["documents"] = documents.Count,
                ["chunks"] = chunks.Count,
                ["vector_dimension"] = vectorSize
            },
            DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        var queryEmbedding = await ExecuteStageAsync(
                "Generating probe embedding...",
                showSpinner: true,
                innerCancellationToken => _embedder.EmbedAsync(ProbeQuestion, innerCancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
        var queryVector = new ReadOnlyMemory<float>(queryEmbedding.Values.ToArray());
        var probeResults = await ExecuteStageAsync(
                "Running Gate A probe retrieval...",
                showSpinner: true,
                innerCancellationToken => _retriever.SearchAsync(queryVector, _config.RetrievalN, innerCancellationToken),
                cancellationToken)
            .ConfigureAwait(false);

        var top10 = probeResults
            .Take(10)
            .Select(result => new GateAProbeResult(
                result.DocId,
                BuildChunkId(result.DocId, result.ChunkIndex),
                result.ChunkIndex,
                result.Score))
            .ToList();

        if (string.IsNullOrWhiteSpace(_config.GateATargetDocId))
        {
            throw new InvalidOperationException("GateATargetDocId is required for the probe.");
        }

        var doc6InTop3 = probeResults
            .Take(_config.SelectionK)
            .Any(result => string.Equals(result.DocId, _config.GateATargetDocId, StringComparison.Ordinal));

        var artifact = new GateAProbeArtifact(
            ProbeQuestion,
            DateTimeOffset.UtcNow.ToString("O"),
            _config.EmbeddingModel,
            _collectionName,
            top10,
            doc6InTop3);

        await _probeWriter.WriteAsync(artifact, cancellationToken).ConfigureAwait(false);

        _logger
            .WithProperties(
                ("scenario_id", scenarioId),
                ("probe_result_count", top10.Count),
                ("doc6_in_top3", doc6InTop3))
            .Debug("Gate A probe completed");

        await _traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.GateAProbeCompleted,
            runId,
            scenarioId,
            2,
            new Dictionary<string, object?>
            {
                ["doc6_in_top3"] = doc6InTop3,
                ["results"] = top10.Count
            },
            DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return new EmbeddingPipelineResult(
            documents.Count,
            chunks.Count,
            vectorSize,
            doc6InTop3,
            runId);
    }

    private static string BuildRunId(string prefix)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var shortGuid = Guid.NewGuid().ToString("N")[..4];
        return $"{prefix}_{timestamp}_{shortGuid}";
    }

    private static string BuildChunkId(string docId, int chunkIndex)
    {
        return $"{docId}_{chunkIndex}";
    }

    private Task<T> ExecuteStageAsync<T>(
        string stageMessage,
        bool showSpinner,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        return _stageProgressReporter is null
            ? action(cancellationToken)
            : _stageProgressReporter.ExecuteStageAsync(stageMessage, showSpinner, action, cancellationToken);
    }
}

public sealed record EmbeddingPipelineResult(
    int DocumentCount,
    int ChunkCount,
    int VectorDimension,
    bool Doc6InTop3,
    string RunId);
