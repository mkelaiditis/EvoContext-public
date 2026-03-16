using System;
using System.Collections.Generic;
using EvoContext.Core.Documents;

namespace EvoContext.Infrastructure.Services;

public sealed class Chunker : IChunker
{
    private readonly int _chunkSizeChars;
    private readonly int _chunkOverlapChars;

    public Chunker(int chunkSizeChars, int chunkOverlapChars)
    {
        _chunkSizeChars = chunkSizeChars;
        _chunkOverlapChars = chunkOverlapChars;
    }

    public IReadOnlyList<DocumentChunk> Chunk(SourceDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var normalizedText = TextNormalization.NormalizeLineEndings(document.Content ?? string.Empty);
        return DocumentChunking.CreateChunks(document.DocumentId, normalizedText, _chunkSizeChars, _chunkOverlapChars);
    }
}
