using EvoContext.Core.Documents;
using EvoContext.Core.Embeddings;

namespace EvoContext.Core.VectorStore;

public interface IVectorIndex
{
    Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken = default);
    Task UpsertAsync(IReadOnlyList<EmbeddingVector> vectors, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);
}
