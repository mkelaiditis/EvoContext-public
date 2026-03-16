using System.Collections.Generic;
using System.Linq;
using EvoContext.Core.Documents;
using Serilog;

namespace EvoContext.Cli;

internal static class IngestionLogger
{
    public static void WriteDocumentDetails(
        ILogger logger,
        IReadOnlyList<PolicyDocument> documents,
        IReadOnlyList<DocumentChunk> chunks)
    {
        var chunkCounts = chunks
            .GroupBy(chunk => chunk.DocumentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (var document in documents.OrderBy(doc => doc.DocId, StringComparer.Ordinal))
        {
            var chunkCount = chunkCounts.TryGetValue(document.DocId, out var count) ? count : 0;
            logger.Information(
                "doc_id={DocId} char_length={CharLength} chunk_count={ChunkCount}",
                document.DocId,
                document.NormalizedText.Length,
                chunkCount);
        }
    }
}
