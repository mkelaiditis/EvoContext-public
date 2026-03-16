namespace EvoContext.Core.Evaluation;

internal static class Phase4PatternMatcher
{
    private const int NegationGuardWindowSize = 100;

    public static IReadOnlyList<string> NormalizePatterns(IReadOnlyList<string>? patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return Array.Empty<string>();
        }

        return patterns
            .Select(Phase4TextNormalizer.Normalize)
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .ToList();
    }

    public static bool ContainsAny(string normalizedText, IReadOnlyList<string> normalizedPatterns)
    {
        foreach (var pattern in normalizedPatterns)
        {
            if (normalizedText.Contains(pattern, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsAnyAffirmative(
        string normalizedText,
        IReadOnlyList<string> normalizedPatterns,
        IReadOnlyList<string> normalizedNegationGuards)
    {
        if (normalizedNegationGuards.Count == 0)
        {
            return ContainsAny(normalizedText, normalizedPatterns);
        }

        foreach (var pattern in normalizedPatterns)
        {
            var searchStart = 0;

            while (searchStart < normalizedText.Length)
            {
                var matchIndex = normalizedText.IndexOf(pattern, searchStart, StringComparison.Ordinal);
                if (matchIndex < 0)
                {
                    break;
                }

                var windowStart = Math.Max(0, matchIndex - NegationGuardWindowSize);
                var windowEnd = Math.Min(
                    normalizedText.Length,
                    matchIndex + pattern.Length + NegationGuardWindowSize);
                var window = normalizedText.Substring(windowStart, windowEnd - windowStart);

                if (!ContainsAny(window, normalizedNegationGuards))
                {
                    return true;
                }

                searchStart = matchIndex + pattern.Length;
            }
        }

        return false;
    }
}
