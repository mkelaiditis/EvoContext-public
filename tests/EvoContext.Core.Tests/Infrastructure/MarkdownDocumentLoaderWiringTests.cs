using EvoContext.Core.Documents;
using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests.Infrastructure;

public sealed class MarkdownDocumentLoaderWiringTests
{
    [Fact]
    public async Task MarkdownDocumentLoader_LoadAsync_MatchesDocumentIngestionOutput()
    {
        var loader = new MarkdownDocumentLoader();
        var ingestion = new DocumentIngestionService();

        var loaded = await loader.LoadAsync(TestDatasetPaths.PolicyDocsPath, TestContext.Current.CancellationToken);
        var ingested = await ingestion.LoadDocumentsAsync(TestDatasetPaths.PolicyDocsPath, TestContext.Current.CancellationToken);

        Assert.Equal(ingested.Count, loaded.Count);
        Assert.NotEmpty(loaded);

        for (var i = 0; i < ingested.Count; i++)
        {
            Assert.Equal(ingested[i].DocId, loaded[i].DocumentId);
            Assert.Equal(ingested[i].NormalizedText, loaded[i].Content);
            Assert.Equal(ingested[i].Title, loaded[i].Title);
        }
    }

    [Fact]
    public void EmbeddingPipelineService_UsesIDatasetLoaderInConstructor()
    {
        var constructor = typeof(EmbeddingPipelineService)
            .GetConstructors()
            .Single();

        var documentLoaderParameter = constructor.GetParameters()[2];

        Assert.Equal(typeof(IDatasetLoader), documentLoaderParameter.ParameterType);
    }
}
