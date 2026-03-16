using System;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Tests;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Phase4FormatValidatorTests
{
    [Fact]
    public void Validate_ReturnsValid_WhenStructureAndWordCountWithinRange()
    {
        var answer = TestAnswerBuilder.BuildAnswer(170);

        var result = Phase4FormatValidator.Validate(answer);

        Assert.True(result.IsValid);
        Assert.True(result.HasRequiredStructure);
        Assert.True(result.WordCountWithinRange);
    }

    [Fact]
    public void Validate_ReturnsInvalid_WhenHeadingMissing()
    {
        var answer = "A. Summary\nOnly one section present.";

        var result = Phase4FormatValidator.Validate(answer);

        Assert.False(result.IsValid);
        Assert.False(result.HasRequiredStructure);
    }

    [Fact]
    public void Validate_ReturnsOutOfRange_WhenBelowMinimum()
    {
        var answer = TestAnswerBuilder.BuildAnswer(10);

        var result = Phase4FormatValidator.Validate(answer);

        Assert.False(result.IsValid);
        Assert.False(result.WordCountWithinRange);
        Assert.True(result.WordCount < Phase4Constants.MinAnswerWords);
    }

    [Fact]
    public void Validate_ReturnsOutOfRange_WhenAboveMaximum()
    {
        var answer = TestAnswerBuilder.BuildAnswer(260);

        var result = Phase4FormatValidator.Validate(answer);

        Assert.False(result.IsValid);
        Assert.False(result.WordCountWithinRange);
        Assert.True(result.WordCount > Phase4Constants.MaxAnswerWords);
    }

    [Fact]
    public void Validate_ReturnsValid_WhenWordCountAtMinimum()
    {
        const int fixedWordCount = 19;
        var answer = TestAnswerBuilder.BuildAnswer(Phase4Constants.MinAnswerWords - fixedWordCount);

        var result = Phase4FormatValidator.Validate(answer);

        Assert.True(result.IsValid);
        Assert.True(result.WordCountWithinRange);
        Assert.Equal(Phase4Constants.MinAnswerWords, result.WordCount);
    }

    [Fact]
    public void Validate_ReturnsValid_WhenWordCountAtMaximum()
    {
        const int fixedWordCount = 19;
        var answer = TestAnswerBuilder.BuildAnswer(Phase4Constants.MaxAnswerWords - fixedWordCount);

        var result = Phase4FormatValidator.Validate(answer);

        Assert.True(result.IsValid);
        Assert.True(result.WordCountWithinRange);
        Assert.Equal(Phase4Constants.MaxAnswerWords, result.WordCount);
    }

    [Fact]
    public void Validate_ReturnsInvalid_WhenWordCountBelowMinimumByOne()
    {
        const int fixedWordCount = 19;
        var answer = TestAnswerBuilder.BuildAnswer(Phase4Constants.MinAnswerWords - fixedWordCount - 1);

        var result = Phase4FormatValidator.Validate(answer);

        Assert.False(result.IsValid);
        Assert.False(result.WordCountWithinRange);
        Assert.Equal(Phase4Constants.MinAnswerWords - 1, result.WordCount);
    }

    [Fact]
    public void Validate_ReturnsInvalid_WhenWordCountAboveMaximumByOne()
    {
        const int fixedWordCount = 19;
        var answer = TestAnswerBuilder.BuildAnswer(Phase4Constants.MaxAnswerWords - fixedWordCount + 1);

        var result = Phase4FormatValidator.Validate(answer);

        Assert.False(result.IsValid);
        Assert.False(result.WordCountWithinRange);
        Assert.Equal(Phase4Constants.MaxAnswerWords + 1, result.WordCount);
    }

    [Fact]
    public void Validate_ReturnsInvalid_WhenHeadingsOutOfOrder()
    {
        var answer = string.Join(
            "\n\n",
            "D. Timeline and Process\nprocess",
            "A. Summary\nsummary",
            "B. Eligibility Rules\nrule",
            "C. Exceptions\nexception");

        var result = Phase4FormatValidator.Validate(answer);

        Assert.False(result.IsValid);
        Assert.False(result.HasRequiredStructure);
    }

    [Fact]
    public void Validate_ThrowsForNullAnswer()
    {
        Assert.Throws<ArgumentNullException>(() => Phase4FormatValidator.Validate(null!));
    }
}
