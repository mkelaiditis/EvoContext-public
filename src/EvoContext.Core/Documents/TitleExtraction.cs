using System;

namespace EvoContext.Core.Documents;

public static class TitleExtraction
{
    public static string ExtractFirstH1(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line.Substring(2);
            }

            if (line.StartsWith("#", StringComparison.Ordinal)
                && !line.StartsWith("##", StringComparison.Ordinal))
            {
                return line.Length > 1 ? line.Substring(1) : string.Empty;
            }
        }

        return string.Empty;
    }
}
