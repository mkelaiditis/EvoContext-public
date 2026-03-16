using System.Text;
using EvoContext.Core.Documents;

namespace EvoContext.Core.Evaluation;

public static class Phase4TextNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = TextNormalization.NormalizeLineEndings(text)
            .Normalize(NormalizationForm.FormKC)
            .ToLowerInvariant();

        var buffer = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Append(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                buffer.Append(' ');
                continue;
            }

            if (char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                buffer.Append(' ');
                continue;
            }
        }

        return CollapseWhitespace(buffer.ToString());
    }

    private static string CollapseWhitespace(string text)
    {
        var buffer = new StringBuilder(text.Length);
        var lastWasSpace = false;

        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    buffer.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            buffer.Append(ch);
            lastWasSpace = false;
        }

        return buffer.ToString().Trim();
    }
}
