namespace EvoContext.Core.Evaluation;

public sealed record FormatValidationResult(
    bool HasRequiredStructure,
    int WordCount,
    bool WordCountWithinRange)
{
    public bool IsValid => HasRequiredStructure && WordCountWithinRange;
}

public static class Phase4FormatValidator
{
    public static FormatValidationResult Validate(string answer)
    {
        if (answer is null)
        {
            throw new ArgumentNullException(nameof(answer));
        }

        var wordCount = CountWords(answer);
        var withinRange = wordCount is >= Phase4Constants.MinAnswerWords and <= Phase4Constants.MaxAnswerWords;
        var hasStructure = HasRequiredStructure(answer);

        return new FormatValidationResult(hasStructure, wordCount, withinRange);
    }

    private static bool HasRequiredStructure(string answer)
    {
        var summaryIndex = answer.IndexOf("A. Summary", StringComparison.Ordinal);
        var eligibilityIndex = answer.IndexOf("B. Eligibility Rules", StringComparison.Ordinal);
        var exceptionsIndex = answer.IndexOf("C. Exceptions", StringComparison.Ordinal);
        var timelineIndex = answer.IndexOf("D. Timeline and Process", StringComparison.Ordinal);

        return summaryIndex >= 0
            && eligibilityIndex > summaryIndex
            && exceptionsIndex > eligibilityIndex
            && timelineIndex > exceptionsIndex;
    }

    private static int CountWords(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return 0;
        }

        return answer.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
