namespace EvoContext.Core.Text;

public static class StringExtensions
{
    public static bool HasValue(this string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    public static string? NormalizeOptional(this string? value)
    {
        if (!value.HasValue())
        {
            return null;
        }

        return value!.Trim();
    }
}