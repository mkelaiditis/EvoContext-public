using System.Collections.Generic;
using System.Linq;
using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Phase4EvaluatorScoringTests
{
    [Fact]
    public void Evaluate_ReturnsFullScore_WhenAllFactsGrounded()
    {
        var evaluator = new Phase4Evaluator();
        var input = BuildInput(BuildAnswer(includeBillingError: true, includeProcessingTimeline: true));

        var result = evaluator.Evaluate(input);
        var policyResult = Assert.IsType<PolicyRefundScenarioResult>(result.ScenarioResult);

        Assert.Equal(100, result.ScoreTotal);
        Assert.Equal(50, policyResult.ScoreBreakdown.CompletenessPoints);
        Assert.Equal(Phase4Constants.FormatPoints, policyResult.ScoreBreakdown.FormatPoints);
        Assert.Equal(0, policyResult.ScoreBreakdown.HallucinationPenalty);
        Assert.False(policyResult.ScoreBreakdown.AccuracyCapApplied);
        Assert.Equal(5, policyResult.PresentFactLabels.Count);
        Assert.Empty(policyResult.MissingFactLabels);
        Assert.Empty(policyResult.HallucinationFlags);
        Assert.Empty(result.QuerySuggestions);
    }

    [Fact]
    public void Evaluate_ReturnsMissingLabels_WhenF3AndF4Absent()
    {
        var evaluator = new Phase4Evaluator();
        var input = BuildInput(BuildAnswer(includeBillingError: false, includeProcessingTimeline: false));

        var result = evaluator.Evaluate(input);
        var policyResult = Assert.IsType<PolicyRefundScenarioResult>(result.ScenarioResult);

        Assert.Equal(80, result.ScoreTotal);
        Assert.Equal(30, policyResult.ScoreBreakdown.CompletenessPoints);
        Assert.Equal(3, policyResult.PresentFactLabels.Count);
        Assert.Collection(
            policyResult.MissingFactLabels,
            first => Assert.Equal(Phase4RuleTables.MissingBillingErrorException, first),
            second => Assert.Equal(Phase4RuleTables.MissingProcessingTimeline, second));
    }

    private static EvaluationInput BuildInput(string answerText)
    {
        return new EvaluationInput(
            "run-001",
            "policy_refund_v1",
            answerText,
            new List<SelectedChunk>
            {
                new(
                    "doc-01",
                    "doc-01-000",
                    0,
                    string.Join(
                        " ",
                        "14-day cooling-off period",
                        "prorated reimbursement",
                        "unused service value",
                        "billing errors",
                        "processed within 5-10 business days",
                        "account portal",
                        "account identifier"))
            });
    }

    private static string BuildAnswer(bool includeBillingError, bool includeProcessingTimeline)
    {
        var filler = string.Join(" ", Enumerable.Repeat("word", 150));
        var summary = $"A. Summary\n{filler} within 14 days cooling-off.";
        var eligibility = "B. Eligibility Rules\nEligibility covers prorated refund for unused month early termination.";
        var exceptions = includeBillingError
            ? "C. Exceptions\nduplicate charge billing error allows adjustment."
            : "C. Exceptions\nno explicit exception coverage.";
        var timeline = includeProcessingTimeline
            ? "D. Timeline and Process\nrefund processed within 5-10 business days; cancel via portal with account ID."
            : "D. Timeline and Process\nrefunds are handled after cancel via portal with account ID.";

        return string.Join("\n\n", summary, eligibility, exceptions, timeline);
    }
}
