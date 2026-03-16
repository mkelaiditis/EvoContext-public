using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EvoContext.Core.Documents;

namespace EvoContext.Infrastructure.Services;

public sealed class MarkdownDocumentLoader : IDatasetLoader
{
    public async Task<IReadOnlyList<SourceDocument>> LoadAsync(string datasetLocation, CancellationToken cancellationToken = default)
    {
        var ingestion = new DocumentIngestionService();
        var documents = await ingestion.LoadDocumentsAsync(datasetLocation, cancellationToken).ConfigureAwait(false);

        return documents
            .Select(document => new SourceDocument(
                document.DocId,
                document.NormalizedText,
                document.Title,
                ToStringMetadata(document.Metadata)))
            .ToList();
    }

    private static IReadOnlyDictionary<string, string> ToStringMetadata(
        IReadOnlyDictionary<string, object> metadata)
    {
        return metadata.ToDictionary(
            pair => pair.Key,
            pair => Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty,
            StringComparer.Ordinal);
    }
}
