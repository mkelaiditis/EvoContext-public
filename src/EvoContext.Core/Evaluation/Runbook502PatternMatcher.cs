namespace EvoContext.Core.Evaluation;

internal static class Runbook502PatternMatcher
{
    public static IReadOnlyList<string> NormalizePatterns(IReadOnlyList<string>? patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return Array.Empty<string>();
        }

        return patterns
            .Select(NormalizePattern)
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .ToList();
    }

    public static bool ContainsAny(string normalizedText, IReadOnlyList<string> normalizedPatterns)
    {
        foreach (var pattern in normalizedPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    normalizedText,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                return true;
            }
        }

        return false;
    }

    public static int FirstIndexOfAny(string normalizedText, IReadOnlyList<string> normalizedPatterns)
    {
        var firstIndex = -1;

        foreach (var pattern in normalizedPatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                normalizedText,
                pattern,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            if (firstIndex < 0 || match.Index < firstIndex)
            {
                firstIndex = match.Index;
            }
        }

        return firstIndex;
    }

    private static string NormalizePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return string.Empty;
        }

        var normalized = pattern.Trim().ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.Replace(normalized, "\\s+", " ");
    }
}
