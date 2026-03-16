namespace EvoContext.Infrastructure.Services;

public sealed record AnswerFormatValidationResult(
    bool HasRequiredStructure,
    int WordCount,
    bool WordCountWithinRange);

public sealed class AnswerFormatValidator
{
    public AnswerFormatValidationResult Validate(string answer)
    {
        if (answer is null)
        {
            throw new ArgumentNullException(nameof(answer));
        }

        var wordCount = CountWords(answer);
        var withinRange = wordCount is >= 150 and <= 250;
        var hasStructure = HasRequiredStructure(answer);

        return new AnswerFormatValidationResult(hasStructure, wordCount, withinRange);
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
