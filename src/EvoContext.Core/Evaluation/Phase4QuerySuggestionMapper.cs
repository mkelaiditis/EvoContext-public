namespace EvoContext.Core.Evaluation;

public sealed class Phase4QuerySuggestionMapper
{
    public IReadOnlyList<string> Map(IReadOnlyList<string> missingLabels)
    {
        if (missingLabels is null)
        {
            throw new ArgumentNullException(nameof(missingLabels));
        }

        var suggestions = new List<string>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var label in missingLabels)
        {
            if (!Phase4RuleTables.QuerySuggestions.TryGetValue(label, out var queries))
            {
                continue;
            }

            foreach (var query in queries)
            {
                var key = Phase4TextNormalizer.Normalize(query);
                if (!seenKeys.Add(key))
                {
                    continue;
                }

                suggestions.Add(query);
                if (suggestions.Count >= Phase4Constants.MaxQuerySuggestions)
                {
                    return suggestions;
                }
            }
        }

        return suggestions;
    }
}
