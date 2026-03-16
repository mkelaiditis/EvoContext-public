using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Phase4FactEvaluatorGroundingTests
{
    [Fact]
    public void Evaluate_DetectsF1WhenAnswerAndContextMatch()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "within 14 days cooling-off",
            "refund within 14 days");

        Assert.Collection(result.PresentFactIds, fact => Assert.Equal("F1", fact));
        Assert.Collection(
            result.MissingLabels,
            first => Assert.Equal(Phase4RuleTables.MissingAnnualProrationRule, first),
            second => Assert.Equal(Phase4RuleTables.MissingBillingErrorException, second),
            third => Assert.Equal(Phase4RuleTables.MissingProcessingTimeline, third),
            fourth => Assert.Equal(Phase4RuleTables.MissingCancellationProcedure, fourth));
    }

    [Fact]
    public void Evaluate_DoesNotCountF1WhenAnswerPresentButContextMissingAnchor()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "within 14 days cooling-off",
            "context without anchors");

        Assert.DoesNotContain("F1", result.PresentFactIds);
        Assert.Contains(Phase4RuleTables.MissingCoolingOffWindow, result.MissingLabels);
    }

    [Fact]
    public void Evaluate_DetectsF2WhenAnswerAndContextMatch()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "eligible for prorated reimbursement for unused months",
            "prorated reimbursement");

        Assert.Collection(result.PresentFactIds, fact => Assert.Equal("F2", fact));
        Assert.Collection(
            result.MissingLabels,
            first => Assert.Equal(Phase4RuleTables.MissingCoolingOffWindow, first),
            second => Assert.Equal(Phase4RuleTables.MissingBillingErrorException, second),
            third => Assert.Equal(Phase4RuleTables.MissingProcessingTimeline, third),
            fourth => Assert.Equal(Phase4RuleTables.MissingCancellationProcedure, fourth));
    }

    [Fact]
    public void Evaluate_DoesNotCountF2WhenProrationIsNegatedAsNotSpecified()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "no prorated reimbursement mechanism is specified",
            "prorated reimbursement");

        Assert.DoesNotContain("F2", result.PresentFactIds);
        Assert.Contains(Phase4RuleTables.MissingAnnualProrationRule, result.MissingLabels);
    }

    [Fact]
    public void Evaluate_DoesNotCountF2WhenProrationIsNegatedAsDoesNotMention()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "the provided context does not mention prorated reimbursement for unused months",
            "prorated reimbursement");

        Assert.DoesNotContain("F2", result.PresentFactIds);
        Assert.Contains(Phase4RuleTables.MissingAnnualProrationRule, result.MissingLabels);
    }

    [Fact]
    public void Evaluate_DoesNotCountF2WhenEarlyTerminationRefundIsNegated()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "no early termination refund clause exists",
            "contract year termination");

        Assert.DoesNotContain("F2", result.PresentFactIds);
        Assert.Contains(Phase4RuleTables.MissingAnnualProrationRule, result.MissingLabels);
    }

    [Fact]
    public void Evaluate_DetectsF2WhenAnswerAffirmsProratedReimbursementForUnusedMonths()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "customers may receive prorated reimbursement for unused months",
            "unused service value");

        Assert.Collection(result.PresentFactIds, fact => Assert.Equal("F2", fact));
    }

    [Fact]
    public void Evaluate_DetectsF2WhenProratedReimbursementHasNoNegationGuard()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "prorated reimbursement",
            "prorated reimbursement");

        Assert.Collection(result.PresentFactIds, fact => Assert.Equal("F2", fact));
    }

    [Fact]
    public void Evaluate_DetectsF3WhenAnswerAndContextMatch()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "billing error correction",
            "billing errors only");

        Assert.Collection(result.PresentFactIds, fact => Assert.Equal("F3", fact));
        Assert.Collection(
            result.MissingLabels,
            first => Assert.Equal(Phase4RuleTables.MissingCoolingOffWindow, first),
            second => Assert.Equal(Phase4RuleTables.MissingAnnualProrationRule, second),
            third => Assert.Equal(Phase4RuleTables.MissingProcessingTimeline, third),
            fourth => Assert.Equal(Phase4RuleTables.MissingCancellationProcedure, fourth));
    }

    [Fact]
    public void Evaluate_DoesNotCountF3WhenAnswerPresentButContextMissingAnchor()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "billing error correction",
            "context without anchors");

        Assert.DoesNotContain("F3", result.PresentFactIds);
        Assert.Contains(Phase4RuleTables.MissingBillingErrorException, result.MissingLabels);
    }

    [Fact]
    public void Evaluate_DetectsF4WhenAnswerAndContextMatch()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "five to ten business days",
            "refund timeline only");

        Assert.Collection(result.PresentFactIds, fact => Assert.Equal("F4", fact));
        Assert.Collection(
            result.MissingLabels,
            first => Assert.Equal(Phase4RuleTables.MissingCoolingOffWindow, first),
            second => Assert.Equal(Phase4RuleTables.MissingAnnualProrationRule, second),
            third => Assert.Equal(Phase4RuleTables.MissingBillingErrorException, third),
            fourth => Assert.Equal(Phase4RuleTables.MissingCancellationProcedure, fourth));
    }

    [Fact]
    public void Evaluate_DoesNotCountF4WhenAnswerPresentButContextMissingAnchor()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "five to ten business days",
            "context without anchors");

        Assert.DoesNotContain("F4", result.PresentFactIds);
        Assert.Contains(Phase4RuleTables.MissingProcessingTimeline, result.MissingLabels);
    }

    [Fact]
    public void Evaluate_DetectsF5WhenAnswerAndContextMatch()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "cancel via portal with account id",
            "account portal account identifier");

        Assert.Collection(result.PresentFactIds, fact => Assert.Equal("F5", fact));
        Assert.Collection(
            result.MissingLabels,
            first => Assert.Equal(Phase4RuleTables.MissingCoolingOffWindow, first),
            second => Assert.Equal(Phase4RuleTables.MissingAnnualProrationRule, second),
            third => Assert.Equal(Phase4RuleTables.MissingBillingErrorException, third),
            fourth => Assert.Equal(Phase4RuleTables.MissingProcessingTimeline, fourth));
    }

    [Fact]
    public void Evaluate_DoesNotCountF5WhenAnswerPresentButContextMissingAnchor()
    {
        var evaluator = new Phase4FactEvaluator();

        var result = evaluator.Evaluate(
            "cancel via portal with account id",
            "context without anchors");

        Assert.DoesNotContain("F5", result.PresentFactIds);
        Assert.Contains(Phase4RuleTables.MissingCancellationProcedure, result.MissingLabels);
    }
}
