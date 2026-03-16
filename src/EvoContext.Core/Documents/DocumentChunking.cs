using System;
using System.Collections.Generic;
using EvoContext.Core.Text;
namespace EvoContext.Core.Documents;

public static class DocumentChunking
{
    public static IReadOnlyList<DocumentChunk> CreateChunks(
        string docId,
        string normalizedText,
        int chunkSizeChars,
        int chunkOverlapChars,
        string? documentTitle = null)
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

        var normalizedDocumentTitle = documentTitle.NormalizeOptional();

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

            var section = DeriveSection(text, start, normalizedDocumentTitle);

            chunks.Add(new DocumentChunk(
                chunkId,
                docId,
                chunkIndex,
                start,
                end,
                chunkText,
                metadata,
                normalizedDocumentTitle,
                section));

            if (end >= text.Length)
            {
                break;
            }

            start = end - overlap;
            chunkIndex++;
        }

        return chunks;
    }

    internal static string DeriveSection(string normalizedText, int chunkStartChar, string? documentTitle)
    {
        var text = normalizedText ?? string.Empty;
        var boundedStart = Math.Clamp(chunkStartChar, 0, text.Length);

        var h3 = FindNearestHeading(text, boundedStart, level: 3);
        if (h3.HasValue())
        {
            return h3!;
        }

        var h2 = FindNearestHeading(text, boundedStart, level: 2);
        if (h2.HasValue())
        {
            return h2!;
        }

        var normalizedDocumentTitle = documentTitle.NormalizeOptional();
        return normalizedDocumentTitle ?? "General";
    }

    private static string? FindNearestHeading(string text, int maxStartExclusive, int level)
    {
        string? nearest = null;
        var lineStart = 0;

        while (lineStart < text.Length && lineStart <= maxStartExclusive)
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            var line = text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');
            if (TryExtractHeading(line, level, out var heading))
            {
                nearest = heading;
            }

            if (lineEnd == text.Length)
            {
                break;
            }

            lineStart = lineEnd + 1;
        }

        return nearest.NormalizeOptional();
    }

    private static bool TryExtractHeading(string line, int level, out string heading)
    {
        heading = string.Empty;
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        if (level == 3)
        {
            if (!line.StartsWith("###", StringComparison.Ordinal)
                || line.StartsWith("####", StringComparison.Ordinal))
            {
                return false;
            }

            heading = line.Length > 3 && line[3] == ' '
                ? line.Substring(4)
                : line.Substring(3);
            return heading.HasValue();
        }

        if (level == 2)
        {
            if (!line.StartsWith("##", StringComparison.Ordinal)
                || line.StartsWith("###", StringComparison.Ordinal))
            {
                return false;
            }

            heading = line.Length > 2 && line[2] == ' '
                ? line.Substring(3)
                : line.Substring(2);
            return heading.HasValue();
        }

        return false;
    }
}
