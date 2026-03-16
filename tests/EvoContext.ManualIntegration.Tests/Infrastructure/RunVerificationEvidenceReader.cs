using System.Text.Json;
using EvoContext.Infrastructure.Models;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal sealed record Run1VerificationEvidenceSelectedChunk(
    string DocumentId,
    string ChunkId,
    int ChunkIndex,
    string ChunkText);

internal sealed record Run1VerificationEvidence(
    string RunId,
    string ScenarioId,
    string Query,
    string Answer,
    IReadOnlyList<string> SelectedChunkDocumentIds,
    IReadOnlyList<Run1VerificationEvidenceSelectedChunk> SelectedChunks);

internal sealed class RunVerificationEvidenceReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool TryRead(
        string verificationEvidencePath,
        FieldPathRegistry fieldPathRegistry,
        out Run1VerificationEvidence? evidence,
        out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationEvidencePath);
        ArgumentNullException.ThrowIfNull(fieldPathRegistry);

        try
        {
            var json = File.ReadAllText(verificationEvidencePath);
            var model = JsonSerializer.Deserialize<RunVerificationEvidence>(json, JsonOptions);
            if (model is null)
            {
                evidence = null;
                error = $"Verification evidence could not be parsed: {verificationEvidencePath}";
                return false;
            }

            fieldPathRegistry.Record("run1_answer", "$.run1.answer");
            fieldPathRegistry.Record("run1_selected_chunk_document_ids", "$.run1.selected_chunks[*].document_id");

            var selectedChunks = model.Run1.SelectedChunks
                .Select(chunk => new Run1VerificationEvidenceSelectedChunk(
                    chunk.DocumentId,
                    chunk.ChunkId,
                    chunk.ChunkIndex,
                    chunk.ChunkText))
                .ToArray();

            evidence = new Run1VerificationEvidence(
                model.RunId,
                model.ScenarioId,
                model.Query,
                model.Run1.Answer,
                selectedChunks.Select(chunk => chunk.DocumentId).ToArray(),
                selectedChunks);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            evidence = null;
            error = ex.Message;
            return false;
        }
    }
}