namespace EvoContext.Core.Retrieval;

public sealed record RetrievalRequest(
    string QueryIdentifier,
    string QueryText,
    int RetrievalN);
