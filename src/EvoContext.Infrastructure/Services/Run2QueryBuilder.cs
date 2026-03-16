using EvoContext.Core.AdaptiveMemory;

namespace EvoContext.Infrastructure.Services;

public sealed class Run2QueryBuilder : IRun2QueryBuilder
{
    private const int MaxFeedbackQueries = 6;

    public RetrievalQuerySet Build(string baseQuery, FeedbackOutput feedback)
    {
        if (string.IsNullOrWhiteSpace(baseQuery))
        {
            throw new ArgumentException("Base query is required.", nameof(baseQuery));
        }

        if (feedback is null)
        {
            throw new ArgumentNullException(nameof(feedback));
        }

        var normalizedBase = baseQuery.Trim();
        var feedbackQueries = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { normalizedBase };

        foreach (var suggestion in feedback.QuerySuggestions)
        {
            var trimmed = suggestion.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (!seen.Add(trimmed))
            {
                continue;
            }

            feedbackQueries.Add(trimmed);
            if (feedbackQueries.Count >= MaxFeedbackQueries)
            {
                break;
            }
        }

        var allQueries = new List<string>(feedbackQueries.Count + 1) { normalizedBase };
        allQueries.AddRange(feedbackQueries);

        return new RetrievalQuerySet(
            normalizedBase,
            feedbackQueries.AsReadOnly(),
            allQueries.AsReadOnly());
    }
}
