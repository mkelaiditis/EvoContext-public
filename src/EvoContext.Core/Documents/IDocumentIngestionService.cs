namespace EvoContext.Core.Documents;

public interface IDocumentIngestionService
{
    Task<IReadOnlyList<PolicyDocument>> LoadDocumentsAsync(string folderPath, CancellationToken cancellationToken = default);
    IReadOnlyList<DocumentChunk> ChunkDocuments(
        IReadOnlyList<PolicyDocument> documents,
        int chunkSizeChars,
        int chunkOverlapChars,
        CancellationToken cancellationToken = default);
    Task<IngestResult> IngestAsync(
        string folderPath,
        int chunkSizeChars,
        int chunkOverlapChars,
        CancellationToken cancellationToken = default);
}
