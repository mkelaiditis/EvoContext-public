namespace EvoContext.Core.Retrieval;

public interface IRetriever
{
    Task<IReadOnlyList<RetrievalCandidate>> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken = default);
}
