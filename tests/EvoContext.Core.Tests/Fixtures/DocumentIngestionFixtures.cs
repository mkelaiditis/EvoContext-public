namespace EvoContext.Core.Tests.Fixtures;

internal static class DocumentIngestionFixtures
{
    public const string ValidFilename = "01_subscription_plans_overview.md";
    public const string InvalidFilename = "subscription_plans_overview.md";
    public const string HiddenFilename = ".hidden.md";
    public const string NonMarkdownFilename = "01_subscription_plans_overview.txt";

    public const string TitleLine = "# Subscription Plans Overview";
    public const string BodyLine = "Details line.";

    public const string TextWithCrlf = "# Title\r\nLine1\r\nLine2\r\n";
    public const string TextWithCr = "# Title\rLine1\rLine2\r";
    public const string NormalizedText = "# Title\nLine1\nLine2\n";

    public const string TextWithoutH1 = "No title here\nSecond line";
    public const string EmptyText = "";

    public static string BuildTextWithTitle(string title, string body)
    {
        return $"# {title}\n{body}\n";
    }

    public static string BuildTextWithTitleCrlf(string title, string body)
    {
        return $"# {title}\r\n{body}\r\n";
    }
}
