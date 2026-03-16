using EvoContext.Core.Documents;
using EvoContext.Core.Tests.Fixtures;

namespace EvoContext.Core.Tests;

public sealed class TitleExtractionTests
{
    [Fact]
    public void ExtractFirstH1_ReturnsFirstHeadingWithSpace()
    {
        var text = DocumentIngestionFixtures.BuildTextWithTitle("Primary Title", "Body line");

        var title = TitleExtraction.ExtractFirstH1(text);

        Assert.Equal("Primary Title", title);
    }

    [Fact]
    public void ExtractFirstH1_ReturnsHeadingWithoutSpace()
    {
        var text = "#Primary Title\nSecond line";

        var title = TitleExtraction.ExtractFirstH1(text);

        Assert.Equal("Primary Title", title);
    }

    [Fact]
    public void ExtractFirstH1_IgnoresNonH1Headings()
    {
        var text = "## Secondary\n### Tertiary\nBody";

        var title = TitleExtraction.ExtractFirstH1(text);

        Assert.Equal(string.Empty, title);
    }

    [Fact]
    public void ExtractFirstH1_ReturnsEmptyWhenMissing()
    {
        var title = TitleExtraction.ExtractFirstH1(DocumentIngestionFixtures.TextWithoutH1);

        Assert.Equal(string.Empty, title);
    }
}
