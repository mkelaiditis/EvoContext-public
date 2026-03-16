namespace EvoContext.Core.Retrieval;

public sealed record RetrievalCandidate(
    string QueryIdentifier,
    int RankWithinQuery,
    float RawSimilarityScore,
    float SimilarityScore,
    string DocumentId,
    string ChunkId,
    int ChunkIndex,
    string ChunkText,
    string? DocumentTitle = null,
    string? Section = null,
    string? QueryText = null)
{
    public int ChunkCharLength => ChunkText.Length;
}
