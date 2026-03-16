using System.Collections.Generic;
using System.Linq;
using System.Text;
using EvoContext.Core.Documents;

namespace EvoContext.Cli;

internal static class IngestionSummaryFormatter
{
    public static string Format(
        IReadOnlyList<PolicyDocument> documents,
        int documentsSkipped,
        IReadOnlyList<DocumentChunk> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"documents_loaded: {documents.Count}");
        builder.AppendLine($"documents_skipped: {documentsSkipped}");
        builder.AppendLine($"total_chunks: {chunks.Count}");
        builder.AppendLine();
        builder.AppendLine("doc_ids:");

        foreach (var docId in documents.Select(document => document.DocId).OrderBy(id => id, System.StringComparer.Ordinal))
        {
            builder.AppendLine(docId);
        }

        return builder.ToString();
    }
}
