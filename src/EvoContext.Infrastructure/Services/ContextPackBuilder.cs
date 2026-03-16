using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EvoContext.Core.Documents;

namespace EvoContext.Infrastructure.Services;

public sealed class ContextPackBuilder
{
    private readonly int _contextBudgetChars;

    public ContextPackBuilder(int contextBudgetChars)
    {
        if (contextBudgetChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contextBudgetChars), "Context budget must be positive.");
        }

        _contextBudgetChars = contextBudgetChars;
    }

    public ContextPack Build(IReadOnlyList<DocumentChunk> orderedChunks)
    {
        if (orderedChunks is null)
        {
            throw new ArgumentNullException(nameof(orderedChunks));
        }

        if (orderedChunks.Count == 0)
        {
            return new ContextPack(string.Empty, Array.Empty<DocumentChunk>(), Array.Empty<string>(), 0);
        }

        var selectedChunks = new List<DocumentChunk>(orderedChunks.Count);
        var docIds = new List<string>();
        var contentBuilder = new StringBuilder();
        var totalCharacters = 0;

        foreach (var chunk in orderedChunks)
        {
            var chunkContent = chunk.Text ?? string.Empty;
            if (chunkContent.Length == 0)
            {
                continue;
            }

            var nextLength = totalCharacters + chunkContent.Length;
            if (nextLength > _contextBudgetChars)
            {
                break;
            }

            if (contentBuilder.Length > 0)
            {
                contentBuilder.Append("\n\n");
                nextLength += 2;
                if (nextLength > _contextBudgetChars)
                {
                    break;
                }
            }

            selectedChunks.Add(chunk);
            if (!docIds.Contains(chunk.DocumentId, StringComparer.Ordinal))
            {
                docIds.Add(chunk.DocumentId);
            }

            contentBuilder.Append(chunkContent);
            totalCharacters = contentBuilder.Length;
        }

        return new ContextPack(
            contentBuilder.ToString(),
            selectedChunks,
            docIds,
            totalCharacters);
    }
}
