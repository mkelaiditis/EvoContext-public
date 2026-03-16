namespace EvoContext.Core.Context;

public sealed record ContextPack(
    string Text,
    int CharCount,
    int ChunkCount,
    int BudgetChars);
