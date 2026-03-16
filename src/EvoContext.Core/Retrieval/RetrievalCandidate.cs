namespace EvoContext.Core.Retrieval;

public sealed record RetrievalCandidate(
    string QueryIdentifier,
    int RankWithinQuery,
    float RawSimilarityScore,
    float SimilarityScore,
    string DocumentId,
    string ChunkId,
    int ChunkIndex,
    string ChunkText)
{
    public int ChunkCharLength => ChunkText.Length;
}
