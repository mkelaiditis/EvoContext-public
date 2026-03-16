using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Phase4ScoreCalculatorTests
{
    [Fact]
    public void Compute_AppliesContradictionCap()
    {
        var calculator = new Phase4ScoreCalculator();

        var result = calculator.Compute(
            50,
            Phase4Constants.FormatPoints,
            0,
            contradictionDetected: true);

        Assert.Equal(Phase4Constants.ContradictionScoreCap, result.ScoreTotal);
        Assert.True(result.Breakdown.AccuracyCapApplied);
    }

    [Fact]
    public void Compute_ClampsScoreToZero()
    {
        var calculator = new Phase4ScoreCalculator();

        var result = calculator.Compute(
            0,
            0,
            -999,
            contradictionDetected: false);

        Assert.Equal(0, result.ScoreTotal);
        Assert.False(result.Breakdown.AccuracyCapApplied);
    }
}
