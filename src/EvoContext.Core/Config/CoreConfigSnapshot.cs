namespace EvoContext.Core.Config;

public sealed record CoreConfigSnapshot(
    string EmbeddingModel,
    string GenerationModel,
    double Temperature,
    double TopP,
    int MaxTokens,
    string DistanceMetric,
    int ChunkSizeChars,
    int ChunkOverlapChars,
    int RetrievalN,
    int SelectionK,
    int ContextBudgetChars,
    string GateATargetDocId);
