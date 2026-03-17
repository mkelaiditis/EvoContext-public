using System.Globalization;
using EvoContext.Core.Text;

namespace EvoContext.Core.AdaptiveMemory;

public static class Run2QueryIdentifier
{
    private const string Prefix = "run2_q";

    public static string Create(int oneBasedQueryOrder)
    {
        if (oneBasedQueryOrder <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(oneBasedQueryOrder), "Query order must be positive.");
        }

        return Prefix + oneBasedQueryOrder.ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string? queryIdentifier, out int oneBasedQueryOrder)
    {
        oneBasedQueryOrder = 0;
        if (!queryIdentifier.HasValue())
        {
            return false;
        }

        if (!queryIdentifier!.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = queryIdentifier.Substring(Prefix.Length);
        if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedOrder))
        {
            return false;
        }

        if (parsedOrder <= 0)
        {
            return false;
        }

        oneBasedQueryOrder = parsedOrder;
        return true;
    }

    public static bool IsFeedbackExpansion(int oneBasedQueryOrder)
    {
        return oneBasedQueryOrder > 1;
    }
}