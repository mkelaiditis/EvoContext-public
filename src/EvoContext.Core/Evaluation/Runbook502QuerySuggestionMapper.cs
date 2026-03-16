namespace EvoContext.Core.Evaluation;

public sealed class Runbook502QuerySuggestionMapper
{
    public IReadOnlyList<string> Map(IReadOnlyList<string> missingStepLabels)
    {
        if (missingStepLabels is null)
        {
            throw new ArgumentNullException(nameof(missingStepLabels));
        }

        var missing = new HashSet<string>(missingStepLabels, StringComparer.Ordinal);
        var suggestions = new List<string>();

        foreach (var stepLabel in Runbook502RuleTables.RequiredStepOrder)
        {
            if (!missing.Contains(stepLabel))
            {
                continue;
            }

            if (Runbook502RuleTables.QuerySuggestionByStep.TryGetValue(stepLabel, out var suggestion))
            {
                suggestions.Add(suggestion);
            }
        }

        return suggestions;
    }
}
