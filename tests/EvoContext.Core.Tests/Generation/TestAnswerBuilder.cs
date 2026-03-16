using System.Linq;

namespace EvoContext.Core.Tests;

internal static class TestAnswerBuilder
{
    public static string BuildAnswer(int wordCount)
    {
        var words = string.Join(" ", Enumerable.Repeat("word", wordCount));
        return string.Join(
            "\n\n",
            "A. Summary\n" + words,
            "B. Eligibility Rules\n- rule",
            "C. Exceptions\n- exception",
            "D. Timeline and Process\n- timeline\n- cancellation");
    }
}
