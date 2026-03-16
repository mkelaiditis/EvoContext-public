using System.Text;
using EvoContext.Core.Context;
using EvoContext.Core.Retrieval;
using CoreContextPack = EvoContext.Core.Context.ContextPack;

namespace EvoContext.Infrastructure.Services;

public sealed class ContextPackPacker : IContextPacker
{
    private readonly int _contextBudgetChars;

    public ContextPackPacker(int contextBudgetChars)
    {
        if (contextBudgetChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contextBudgetChars), "Context budget must be positive.");
        }

        _contextBudgetChars = contextBudgetChars;
    }

    public CoreContextPack Pack(IReadOnlyList<RetrievalCandidate> selectedChunks)
    {
        if (selectedChunks is null)
        {
            throw new ArgumentNullException(nameof(selectedChunks));
        }

        if (selectedChunks.Count == 0)
        {
            return new CoreContextPack(string.Empty, 0, 0, _contextBudgetChars);
        }

        var remaining = selectedChunks.ToList();
        while (remaining.Count > 0)
        {
            var contentBuilder = new StringBuilder();
            var includedCount = 0;

            foreach (var chunk in remaining)
            {
                var chunkContent = chunk.ChunkText;
                if (chunkContent.Length == 0)
                {
                    continue;
                }

                if (contentBuilder.Length > 0)
                {
                    contentBuilder.Append("\n\n");
                }

                contentBuilder.Append(chunkContent);
                includedCount++;
            }

            if (contentBuilder.Length <= _contextBudgetChars)
            {
                return new CoreContextPack(contentBuilder.ToString(), contentBuilder.Length, includedCount, _contextBudgetChars);
            }

            remaining.RemoveAt(remaining.Count - 1);
        }

        return new CoreContextPack(string.Empty, 0, 0, _contextBudgetChars);
    }
}
