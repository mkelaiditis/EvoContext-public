using System.Collections.Generic;

namespace EvoContext.Core.Documents;

public sealed record DocumentChunk(
    string ChunkId,
    string DocumentId,
    int ChunkIndex,
    int StartChar,
    int EndChar,
    string Text,
    IReadOnlyDictionary<string, object> Metadata,
    string? DocumentTitle = null,
    string? Section = null)
{
    public int CharLength => Text.Length;
}
