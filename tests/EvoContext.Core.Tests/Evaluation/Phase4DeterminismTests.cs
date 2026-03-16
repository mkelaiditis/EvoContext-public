using System.Collections.Generic;
using System.Linq;
using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Phase4DeterminismTests
{
    [Fact]
    public void Evaluate_ReturnsIdenticalResults_ForRepeatedInputs()
    {
        var evaluator = new Phase4Evaluator();
        var input = BuildInput();

        var baseline = evaluator.Evaluate(input);

        for (var run = 0; run < 9; run++)
        {
            var current = evaluator.Evaluate(input);
            AssertResultsEqual(baseline, current);
        }
    }

    private static void AssertResultsEqual(EvaluationResult expected, EvaluationResult actual)
    {
        var expectedPolicy = RequirePolicyResult(expected);
        var actualPolicy = RequirePolicyResult(actual);

        Assert.Equal(expected.RunId, actual.RunId);
        Assert.Equal(expected.ScenarioId, actual.ScenarioId);
        Assert.Equal(expected.ScoreTotal, actual.ScoreTotal);
        Assert.Equal(expectedPolicy.ScoreBreakdown, actualPolicy.ScoreBreakdown);
        Assert.Equal(expectedPolicy.PresentFactLabels, actualPolicy.PresentFactLabels);
        Assert.Equal(expectedPolicy.MissingFactLabels, actualPolicy.MissingFactLabels);
        Assert.Equal(expectedPolicy.HallucinationFlags, actualPolicy.HallucinationFlags);
        Assert.Equal(expected.QuerySuggestions, actual.QuerySuggestions);
    }

    private static PolicyRefundScenarioResult RequirePolicyResult(EvaluationResult evaluation)
    {
        return Assert.IsType<PolicyRefundScenarioResult>(evaluation.ScenarioResult);
    }

    private static EvaluationInput BuildInput()
    {
        return new EvaluationInput(
            "run-determinism-001",
            "policy_refund_v1",
            BuildAnswer(),
            new List<SelectedChunk>
            {
                new(
                    "doc-02",
                    "doc-02-000",
                    0,
                    "14-day cooling-off period refund within 14 days prorated reimbursement unused service value " +
                    "service commitment term contract year termination duplicate charges billing errors incorrect charge " +
                    "adjustment refund due to error processed within 5-10 business days refund timeline payment method " +
                    "processing time account portal contact support account identifier cancellation request")
            });
    }

    private static string BuildAnswer()
    {
        var filler = string.Join(" ", Enumerable.Repeat("word", 160));
        var summary = $"A. Summary\n{filler} within 14 days cooling-off.";
        var eligibility = "B. Eligibility Rules\nprorated reimbursement refund for unused month early termination.";
        var exceptions = "C. Exceptions\nduplicate charge billing error correction.";
        var timeline = "D. Timeline and Process\nrefund processed within 5-10 business days cancel via portal with account ID.";

        return string.Join("\n\n", summary, eligibility, exceptions, timeline);
    }
}
