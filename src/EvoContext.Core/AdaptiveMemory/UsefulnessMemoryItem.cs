namespace EvoContext.Core.AdaptiveMemory;

public sealed record UsefulnessMemoryItem(
    string ChunkId,
    string DocumentId,
    int UsefulnessScore,
    string LastUsedRunId);
