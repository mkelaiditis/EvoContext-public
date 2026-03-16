using System;
using System.Collections.Generic;
namespace EvoContext.Core.Documents;

public static class DocumentChunking
{
    public static IReadOnlyList<DocumentChunk> CreateChunks(
        string docId,
        string normalizedText,
        int chunkSizeChars,
        int chunkOverlapChars)
    {
        if (docId is null)
        {
            throw new ArgumentNullException(nameof(docId));
        }

        var text = normalizedText ?? string.Empty;
        if (text.Length == 0)
        {
            return Array.Empty<DocumentChunk>();
        }

        var chunks = new List<DocumentChunk>();
        var chunkSize = chunkSizeChars;
        var overlap = chunkOverlapChars;
        var start = 0;
        var chunkIndex = 0;

        while (start < text.Length)
        {
            var end = Math.Min(start + chunkSize, text.Length);
            if (end <= start)
            {
                break;
            }

            var chunkText = text.Substring(start, end - start);
            var chunkId = $"{docId}_{chunkIndex}";

            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["doc_id"] = docId,
                ["chunk_index"] = chunkIndex,
                ["start_char"] = start,
                ["end_char"] = end
            };

            chunks.Add(new DocumentChunk(chunkId, docId, chunkIndex, start, end, chunkText, metadata));

            if (end >= text.Length)
            {
                break;
            }

            start = end - overlap;
            chunkIndex++;
        }

        return chunks;
    }
}
