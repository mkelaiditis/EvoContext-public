using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests;

public sealed class AnswerFormatValidatorTests
{
    [Fact]
    public void Validate_ReturnsStructureTrue_WhenHeadingsPresentInOrder()
    {
        var validator = new AnswerFormatValidator();
        var answer = TestAnswerBuilder.BuildAnswer(170);

        var result = validator.Validate(answer);

        Assert.True(result.HasRequiredStructure);
    }

    [Fact]
    public void Validate_ReturnsStructureFalse_WhenHeadingsMissing()
    {
        var validator = new AnswerFormatValidator();
        var answer = "A. Summary\nOnly one section present.";

        var result = validator.Validate(answer);

        Assert.False(result.HasRequiredStructure);
    }

    [Fact]
    public void Validate_ReturnsWordCountWithinRange_WhenBetween150And250()
    {
        var validator = new AnswerFormatValidator();
        var answer = TestAnswerBuilder.BuildAnswer(170);

        var result = validator.Validate(answer);

        Assert.True(result.WordCountWithinRange);
        Assert.InRange(result.WordCount, 150, 250);
    }

    [Fact]
    public void Validate_ReturnsWordCountOutOfRange_WhenBelowMinimum()
    {
        var validator = new AnswerFormatValidator();
        var answer = TestAnswerBuilder.BuildAnswer(10);

        var result = validator.Validate(answer);

        Assert.False(result.WordCountWithinRange);
        Assert.True(result.WordCount < 150);
    }

    [Fact]
    public void Validate_ReturnsWordCountOutOfRange_WhenAboveMaximum()
    {
        var validator = new AnswerFormatValidator();
        var answer = TestAnswerBuilder.BuildAnswer(260);

        var result = validator.Validate(answer);

        Assert.False(result.WordCountWithinRange);
        Assert.True(result.WordCount > 250);
    }

}
