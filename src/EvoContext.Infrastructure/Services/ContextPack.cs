using EvoContext.Core.Documents;

namespace EvoContext.Infrastructure.Services;

public sealed class ContextPack
{
    public ContextPack(string content, IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<string> docIds, int totalCharacters)
    {
        Content = content;
        Chunks = chunks;
        DocIds = docIds;
        TotalCharacters = totalCharacters;
    }

    public string Content { get; }
    public IReadOnlyList<DocumentChunk> Chunks { get; }
    public IReadOnlyList<string> DocIds { get; }
    public int TotalCharacters { get; }
}
