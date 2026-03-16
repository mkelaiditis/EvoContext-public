namespace EvoContext.Core.AdaptiveMemory;

public sealed record RetrievalQuerySet(
    string BaseQuery,
    IReadOnlyList<string> FeedbackQueries,
    IReadOnlyList<string> AllQueries);
