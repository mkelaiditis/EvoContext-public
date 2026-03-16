namespace EvoContext.Core.Evaluation;

public sealed record SelectedChunk(
    string DocumentId,
    string ChunkId,
    int ChunkIndex,
    string ChunkText);
