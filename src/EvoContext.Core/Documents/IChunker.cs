namespace EvoContext.Core.Documents;

public interface IChunker
{
    IReadOnlyList<DocumentChunk> Chunk(SourceDocument document);
}
