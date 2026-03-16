namespace EvoContext.Core.Documents;

public sealed record IngestResult(
    IReadOnlyList<PolicyDocument> Documents,
    IReadOnlyList<DocumentChunk> Chunks,
    int DocumentsSkipped,
    IReadOnlyList<string> SkippedFiles);
