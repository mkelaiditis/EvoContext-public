using System;
using System.Linq;
using EvoContext.Infrastructure.Services;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal sealed record RunVerificationArtifact(
    string TraceArtifactPath,
    string RunId,
    int ScoreRun1,
    int? ScoreRun2,
    int? ScoreDelta,
    string Run2Answer,
    IReadOnlyList<string> Run2SelectedChunkDocumentIds);

internal sealed class RunVerificationArtifactReader
{
    private readonly TraceArtifactReader _traceArtifactReader;

    public RunVerificationArtifactReader()
        : this(new TraceArtifactReader())
    {
    }

    public RunVerificationArtifactReader(TraceArtifactReader traceArtifactReader)
    {
        _traceArtifactReader = traceArtifactReader ?? throw new ArgumentNullException(nameof(traceArtifactReader));
    }

    public bool TryRead(
        string traceArtifactPath,
        FieldPathRegistry fieldPathRegistry,
        out RunVerificationArtifact? artifact,
        out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceArtifactPath);
        ArgumentNullException.ThrowIfNull(fieldPathRegistry);

        if (!_traceArtifactReader.TryRead(traceArtifactPath, out var traceArtifact, out error) || traceArtifact is null)
        {
            artifact = null;
            return false;
        }

        fieldPathRegistry.Record("score_run1", "$.score_run1");
        fieldPathRegistry.Record("score_run2", "$.score_run2");
        fieldPathRegistry.Record("score_delta", "$.score_delta");
        fieldPathRegistry.Record("run2_selected_chunk_document_ids", "$.selected_chunks[*].document_id");
        fieldPathRegistry.Record("run2_answer", "$.answer");

        artifact = new RunVerificationArtifact(
            traceArtifactPath,
            traceArtifact.RunId,
            traceArtifact.ScoreRun1,
            traceArtifact.ScoreRun2,
            traceArtifact.ScoreDelta,
            traceArtifact.Answer,
            traceArtifact.SelectedChunks
                .Select(chunk => chunk.DocumentId)
                .Where(static documentId => !string.IsNullOrWhiteSpace(documentId))
                .ToArray());

        error = null;
        return true;
    }
}