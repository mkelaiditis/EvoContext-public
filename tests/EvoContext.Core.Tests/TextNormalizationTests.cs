using EvoContext.Core.Documents;
using EvoContext.Core.Tests.Fixtures;

namespace EvoContext.Core.Tests;

public sealed class TextNormalizationTests
{
    [Theory]
    [InlineData(DocumentIngestionFixtures.TextWithCrlf, DocumentIngestionFixtures.NormalizedText)]
    [InlineData(DocumentIngestionFixtures.TextWithCr, DocumentIngestionFixtures.NormalizedText)]
    public void NormalizeLineEndings_ConvertsToLfOnly(string input, string expected)
    {
        var normalized = TextNormalization.NormalizeLineEndings(input);

        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void NormalizeLineEndings_PreservesLfOnlyText()
    {
        var input = "# Title\nLine1\nLine2\n";
        var normalized = TextNormalization.NormalizeLineEndings(input);

        Assert.Equal(input, normalized);
    }
}
