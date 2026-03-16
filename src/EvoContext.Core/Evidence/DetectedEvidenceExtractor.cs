using EvoContext.Core.Evaluation;
using EvoContext.Core.Retrieval;

namespace EvoContext.Core.Evidence;

public sealed class DetectedEvidenceExtractor
{
    private readonly IReadOnlyList<FactRule> _factRules;
    private readonly IReadOnlyDictionary<string, string> _presentLabelByFactId;

    public DetectedEvidenceExtractor(
        IReadOnlyList<FactRule> factRules,
        IReadOnlyDictionary<string, string> presentLabelByFactId)
    {
        _factRules = factRules ?? throw new ArgumentNullException(nameof(factRules));
        _presentLabelByFactId = presentLabelByFactId ?? throw new ArgumentNullException(nameof(presentLabelByFactId));
    }

    public IReadOnlyList<DetectedEvidenceItem> Extract(IReadOnlyList<RetrievalCandidate> selectedChunks)
    {
        if (selectedChunks is null)
        {
            throw new ArgumentNullException(nameof(selectedChunks));
        }

        var results = new List<DetectedEvidenceItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in _factRules)
        {
            _presentLabelByFactId.TryGetValue(rule.FactId, out var presentLabel);
            presentLabel ??= rule.FactId;

            foreach (var chunk in selectedChunks)
            {
                var key = $"{rule.FactId}|{chunk.DocumentId}";
                if (seen.Contains(key))
                {
                    continue;
                }

                foreach (var anchor in rule.ContextAnchors)
                {
                    var searchStart = 0;

                    while (searchStart < chunk.ChunkText.Length)
                    {
                        var index = chunk.ChunkText.IndexOf(anchor, searchStart, StringComparison.OrdinalIgnoreCase);
                        if (index < 0)
                        {
                            break;
                        }

                        var lineStart = index;
                        while (lineStart > 0 && chunk.ChunkText[lineStart - 1] != '\n')
                        {
                            lineStart--;
                        }

                        if (chunk.ChunkText[lineStart] == '#')
                        {
                            searchStart = index + anchor.Length;
                            continue;
                        }

                        var snippet = ExtractSentence(chunk.ChunkText, index, anchor.Length);
                        results.Add(new DetectedEvidenceItem(
                            chunk.DocumentId,
                            rule.FactId,
                            presentLabel,
                            anchor,
                            snippet));
                        seen.Add(key);
                        break;
                    }

                    if (seen.Contains(key))
                    {
                        break;
                    }
                }
            }
        }

        return results;
    }

    private static string ExtractSentence(string text, int matchIndex, int matchLength)
    {
        var start = matchIndex;
        while (start > 0 && text[start - 1] != '.' && text[start - 1] != '\n')
        {
            start--;
        }

        var end = matchIndex + matchLength;
        while (end < text.Length && text[end] != '.' && text[end] != '\n')
        {
            end++;
        }

        if (end < text.Length && text[end] == '.')
        {
            end++;
        }

        const int MaxSnippetLength = 250;
        var length = Math.Min(end - start, MaxSnippetLength);
        return text.Substring(start, length).Trim();
    }
}
