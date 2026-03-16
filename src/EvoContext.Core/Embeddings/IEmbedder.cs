namespace EvoContext.Core.Embeddings;

public interface IEmbedder
{
    Task<EmbeddingVector> EmbedAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmbeddingVector>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
