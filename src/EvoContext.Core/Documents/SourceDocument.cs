namespace EvoContext.Core.Documents;

public sealed record SourceDocument(
    string DocumentId,
    string Content,
    string? Title = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
