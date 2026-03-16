using System;

namespace EvoContext.Core.Documents;

public static class TextNormalization
{
    public static string NormalizeLineEndings(string text)
    {
        if (text is null)
        {
            return string.Empty;
        }

        if (text.Length == 0)
        {
            return string.Empty;
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }
}
