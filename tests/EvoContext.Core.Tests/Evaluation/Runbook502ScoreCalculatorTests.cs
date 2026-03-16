using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

// Formula: 8 (base) + detectedStepCount * 18 + 10 (list format) - hallucinationPenalty, clamped 0–100
// Targets: 2 steps + list = 54, 4 steps + list = 90

public sealed class Runbook502ScoreCalculatorTests
{
    [Fact]
    public void Compute_ReturnsTwoStepsWithList_Gives54()
    {
        var calculator = new Runbook502ScoreCalculator();

        var result = calculator.Compute(2, 2, false, true, 0);

        Assert.Equal(54, result.ScoreTotal);  // 8 + 36 + 10
    }

    [Fact]
    public void Compute_ReturnsFourStepsWithList_Gives90()
    {
        var calculator = new Runbook502ScoreCalculator();

        var result = calculator.Compute(4, 4, false, true, 0);

        Assert.Equal(90, result.ScoreTotal);  // 8 + 72 + 10
    }

    [Fact]
    public void Compute_ReturnsFourStepsNoList_Gives80()
    {
        var calculator = new Runbook502ScoreCalculator();

        var result = calculator.Compute(4, 4, false, false, 0);

        Assert.Equal(80, result.ScoreTotal);  // 8 + 72 + 0
    }

    [Fact]
    public void Compute_ClampsToOneHundred_WhenFiveStepsDetected()
    {
        var calculator = new Runbook502ScoreCalculator();

        var result = calculator.Compute(5, 5, false, true, 0);

        Assert.Equal(100, result.ScoreTotal);  // 8 + 90 + 10 = 108 → clamped
    }

    [Fact]
    public void Compute_ClampsToZero_WhenHallucinationExceedsTotal()
    {
        var calculator = new Runbook502ScoreCalculator();

        var result = calculator.Compute(0, 0, false, false, 40);

        Assert.Equal(0, result.ScoreTotal);  // 8 + 0 + 0 - 40 = -32 → clamped
        Assert.Equal(40, result.Breakdown.HallucinationPenalty);
    }

    [Fact]
    public void Compute_FormatDeltaIsTen()
    {
        var calculator = new Runbook502ScoreCalculator();

        var withList = calculator.Compute(2, 2, false, true, 0);
        var withoutList = calculator.Compute(2, 2, false, false, 0);

        Assert.Equal(10, withList.ScoreTotal - withoutList.ScoreTotal);
    }

    [Fact]
    public void Compute_OrderViolationFlagDoesNotAffectScore()
    {
        var calculator = new Runbook502ScoreCalculator();

        var noViolation = calculator.Compute(2, 2, false, true, 0);
        var withViolation = calculator.Compute(2, 2, true, true, 0);

        Assert.Equal(noViolation.ScoreTotal, withViolation.ScoreTotal);
    }
}
