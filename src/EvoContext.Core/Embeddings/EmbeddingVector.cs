namespace EvoContext.Core.Embeddings;

public sealed record EmbeddingVector(
    string VectorId,
    string SourceText,
    IReadOnlyList<float> Values);
