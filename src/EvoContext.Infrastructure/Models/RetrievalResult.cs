namespace EvoContext.Infrastructure.Models;

public sealed class RetrievalResult
{
    public RetrievalResult(string docId, int chunkIndex, float score, int rank)
    {
        DocId = docId;
        ChunkIndex = chunkIndex;
        Score = score;
        Rank = rank;
    }

    public string DocId { get; }
    public int ChunkIndex { get; }
    public float Score { get; }
    public int Rank { get; }
}
