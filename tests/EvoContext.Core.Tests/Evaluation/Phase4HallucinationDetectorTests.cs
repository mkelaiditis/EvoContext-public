using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Phase4HallucinationDetectorTests
{
    [Fact]
    public void Evaluate_ReturnsSingleFlagPenalty()
    {
        var detector = new Phase4HallucinationDetector();

        var result = detector.Evaluate("30-day refund period mentioned.");

        Assert.Collection(result.HallucinationFlags,
            flag => Assert.Equal(Phase4RuleTables.HallucinatedTimeWindow, flag));
        Assert.Equal(Phase4Constants.HallucinationPenaltyPerFlag, result.HallucinationPenalty);
    }

    [Fact]
    public void Evaluate_CapsPenaltyWhenMultipleFlagsDetected()
    {
        var detector = new Phase4HallucinationDetector();
        var answer = string.Join(
            " ",
            "30-day refund period",
            "cancellation fee",
            "always refundable",
            "signed form required");

        var result = detector.Evaluate(answer);

        Assert.Collection(
            result.HallucinationFlags,
            first => Assert.Equal(Phase4RuleTables.HallucinatedTimeWindow, first),
            second => Assert.Equal(Phase4RuleTables.HallucinatedFeesOrPenalties, second),
            third => Assert.Equal(Phase4RuleTables.HallucinatedRefundGuarantee, third),
            fourth => Assert.Equal(Phase4RuleTables.HallucinatedExtraRequirements, fourth));
        Assert.Equal(Phase4Constants.HallucinationPenaltyCap, result.HallucinationPenalty);
    }

    [Fact]
    public void Evaluate_ReturnsNoFlagsWhenAnswerIsClean()
    {
        var detector = new Phase4HallucinationDetector();

        var result = detector.Evaluate("A. Summary\nNo hallucinations here.");

        Assert.Empty(result.HallucinationFlags);
        Assert.Equal(0, result.HallucinationPenalty);
    }
}
