using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Runbook502StepEvaluatorTests
{
    [Fact]
    public void EvaluateNormalized_DetectsAllFourSteps_WhenAnswerAndContextAreGrounded()
    {
        var evaluator = new Runbook502StepEvaluator();

        var answer = Phase4TextNormalizer.Normalize(string.Join(" ",
            "First check upstream dependency health and verify dependent services.",
            "Then inspect logs for errors.",
            "Next inspect recent deploy history to identify the cause.",
            "Rollback to previous version if a deployment is faulty."));
        var context = BuildFullGroundedContext();

        var result = evaluator.EvaluateNormalized(answer, context);

        Assert.Equal(4, result.PresentStepLabels.Count);
        Assert.Empty(result.MissingStepLabels);
        Assert.Equal(40, result.StepCoveragePoints);
    }

    [Fact]
    public void EvaluateNormalized_MarksStepMissing_WhenAnswerMentionsStepButContextAnchorMissing()
    {
        var evaluator = new Runbook502StepEvaluator();

        var answer = Phase4TextNormalizer.Normalize("Inspect logs and review log entries for issues.");
        var context = Phase4TextNormalizer.Normalize("This context does not include the runbook anchor sentence.");

        var result = evaluator.EvaluateNormalized(answer, context);

        Assert.Contains(Runbook502RuleTables.StepInspectLogs, result.MissingStepLabels);
        Assert.DoesNotContain(Runbook502RuleTables.StepInspectLogs, result.PresentStepLabels);
    }

    [Fact]
    public void EvaluateNormalized_ProducesOrderViolation_WhenStepAppearsBeforeItsRequiredPredecessor()
    {
        var evaluator = new Runbook502StepEvaluator();

        var answer = Phase4TextNormalizer.Normalize(string.Join(" ",
            "Rollback the deployment immediately.",
            "After that inspect logs for failures."));
        var context = Phase4TextNormalizer.Normalize(string.Join(" ",
            "rollback to the previous stable version",
            "locating the most recent log entries"));

        var result = evaluator.EvaluateNormalized(answer, context);

        Assert.Contains("ORDER_STEP_ROLLBACK_DEPLOYMENT_BEFORE_STEP_INSPECT_LOGS", result.OrderViolationLabels);
    }

    [Fact]
    public void EvaluateNormalized_ProducesNoOrderViolation_WhenOnlyOneStepIsDetected()
    {
        var evaluator = new Runbook502StepEvaluator();
        var answer = Phase4TextNormalizer.Normalize("Check upstream dependency health before proceeding.");
        var context = Phase4TextNormalizer.Normalize("verify that all dependent services are operating correctly");

        var result = evaluator.EvaluateNormalized(answer, context);

        Assert.Single(result.PresentStepLabels);
        Assert.Empty(result.OrderViolationLabels);
    }

    [Fact]
    public void EvaluateNormalized_DetectsCheckUpstreamHealth_WhenBothAnchorAndPatternPresent()
    {
        var evaluator = new Runbook502StepEvaluator();
        var answer = Phase4TextNormalizer.Normalize("Check upstream dependency health before proceeding.");
        var context = Phase4TextNormalizer.Normalize("verify that all dependent services are operating correctly");

        var result = evaluator.EvaluateNormalized(answer, context);

        Assert.Contains(Runbook502RuleTables.StepCheckUpstreamHealth, result.PresentStepLabels);
        Assert.DoesNotContain(Runbook502RuleTables.StepCheckUpstreamHealth, result.MissingStepLabels);
    }

    [Fact]
    public void EvaluateNormalized_DetectsCheckDeployment_WhenBothAnchorAndPatternPresent()
    {
        var evaluator = new Runbook502StepEvaluator();
        var answer = Phase4TextNormalizer.Normalize("Inspect deploy history to identify recent changes.");
        var context = Phase4TextNormalizer.Normalize("inspect the deployment history");

        var result = evaluator.EvaluateNormalized(answer, context);

        Assert.Contains(Runbook502RuleTables.StepCheckDeployment, result.PresentStepLabels);
        Assert.DoesNotContain(Runbook502RuleTables.StepCheckDeployment, result.MissingStepLabels);
    }

    [Fact]
    public void EvaluateNormalized_DetectsRollback_WhenBothAnchorAndPatternPresent()
    {
        var evaluator = new Runbook502StepEvaluator();
        var answer = Phase4TextNormalizer.Normalize("Rollback to the last known good version.");
        var context = Phase4TextNormalizer.Normalize("rollback to the previous stable version");

        var result = evaluator.EvaluateNormalized(answer, context);

        Assert.Contains(Runbook502RuleTables.StepRollbackDeployment, result.PresentStepLabels);
        Assert.DoesNotContain(Runbook502RuleTables.StepRollbackDeployment, result.MissingStepLabels);
    }

    [Fact]
    public void EvaluateNormalized_MarksStepMissing_WhenContextAnchorPresentButAnswerPatternAbsent()
    {
        var evaluator = new Runbook502StepEvaluator();
        var answer = Phase4TextNormalizer.Normalize("The service was deployed last week.");
        var context = Phase4TextNormalizer.Normalize("inspect the deployment history");

        var result = evaluator.EvaluateNormalized(answer, context);

        Assert.Contains(Runbook502RuleTables.StepCheckDeployment, result.MissingStepLabels);
        Assert.DoesNotContain(Runbook502RuleTables.StepCheckDeployment, result.PresentStepLabels);
    }

    [Fact]
    public void EvaluateNormalized_ProducesNoOrderViolation_WhenTwoStepsDetectedInCorrectOrder()
    {
        var evaluator = new Runbook502StepEvaluator();
        var answer = Phase4TextNormalizer.Normalize(string.Join(" ",
            "First check upstream dependency health.",
            "Then inspect logs for errors."));
        var context = Phase4TextNormalizer.Normalize(string.Join(" ",
            "verify that all dependent services are operating correctly",
            "locating the most recent log entries"));

        var result = evaluator.EvaluateNormalized(answer, context);

        Assert.Equal(2, result.PresentStepLabels.Count);
        Assert.Empty(result.OrderViolationLabels);
    }

    private static string BuildFullGroundedContext()
    {
        return Phase4TextNormalizer.Normalize(string.Join(" ",
            "verify that all dependent services are operating correctly",
            "check the health status of each dependency",
            "locating the most recent log entries",
            "begin by locating",
            "inspect the deployment history",
            "recent deployment coincides",
            "rollback to the previous stable version",
            "initiate a rollback"));
    }
}
