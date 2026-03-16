namespace EvoContext.Core.Evidence;

public sealed record DetectedEvidenceItem(
    string DocumentId,
    string FactId,
    string FactLabel,
    string MatchedAnchor,
    string ExtractedSnippet);
