namespace EvoContext.Core.AdaptiveMemory;

public sealed record UsefulnessMemorySnapshot(
    IReadOnlyList<UsefulnessMemoryItem> Items);
