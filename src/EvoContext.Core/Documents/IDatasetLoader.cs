namespace EvoContext.Core.Documents;

public interface IDatasetLoader
{
    Task<IReadOnlyList<SourceDocument>> LoadAsync(string datasetLocation, CancellationToken cancellationToken = default);
}
