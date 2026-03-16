using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Phase4UngroundedFactTests
{
    [Fact]
    public void Evaluate_FlagsUngroundedF2WhenAnswerMentionsProrationWithoutAnchor()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "eligible for prorated reimbursement for unused months",
            "context without anchors");

        Assert.Empty(result.PresentFactIds);
        Assert.True(result.UngroundedF2Detected);
        Assert.Contains(Phase4RuleTables.MissingAnnualProrationRule, result.MissingLabels);
        Assert.Equal(0, result.CompletenessPoints);
    }
}
