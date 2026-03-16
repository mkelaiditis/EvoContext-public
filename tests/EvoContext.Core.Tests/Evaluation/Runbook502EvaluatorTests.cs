using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Runbook502EvaluatorTests
{
    [Fact]
    public void Evaluate_ThrowsInvalidOperationException_WhenScenarioIdIsUnsupported()
    {
        var evaluator = new Runbook502Evaluator();
        var input = BuildInput("policy_refund_v1", "- check logs", "locating the most recent log entries");

        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(input));
    }

    [Fact]
    public void Evaluate_ReturnsAllFourMissingLabels_WhenNoStepsAreDetected()
    {
        var evaluator = new Runbook502Evaluator();
        var input = BuildInput(
            Runbook502Evaluator.RunbookScenarioId,
            "No actionable troubleshooting content is provided.",
            "Irrelevant context without required runbook anchors.");

        var result = evaluator.Evaluate(input);
        var runbookResult = Assert.IsType<Runbook502ScenarioResult>(result.ScenarioResult);

        Assert.Empty(runbookResult.PresentStepLabels);
        Assert.Equal(4, runbookResult.MissingStepLabels.Count);
        Assert.Collection(
            runbookResult.MissingStepLabels,
            first => Assert.Equal(Runbook502RuleTables.StepCheckUpstreamHealth, first),
            second => Assert.Equal(Runbook502RuleTables.StepInspectLogs, second),
            third => Assert.Equal(Runbook502RuleTables.StepCheckDeployment, third),
            fourth => Assert.Equal(Runbook502RuleTables.StepRollbackDeployment, fourth));
    }

    [Fact]
    public void Evaluate_MapsOneSuggestionPerMissingStep_InRequiredOrder()
    {
        var evaluator = new Runbook502Evaluator();

        var answer = string.Join(" ",
            "Check upstream dependencies.",
            "Inspect logs for startup failures.");
        var context = string.Join(" ",
            "verify that all dependent services are operating correctly",
            "locating the most recent log entries");
        var input = BuildInput(Runbook502Evaluator.RunbookScenarioId, answer, context);

        var result = evaluator.Evaluate(input);

        Assert.Equal(2, result.QuerySuggestions.Count);
        Assert.Collection(
            result.QuerySuggestions,
            first => Assert.Equal(Runbook502RuleTables.QuerySuggestionByStep[Runbook502RuleTables.StepCheckDeployment], first),
            second => Assert.Equal(Runbook502RuleTables.QuerySuggestionByStep[Runbook502RuleTables.StepRollbackDeployment], second));
    }

    [Fact]
    public void Evaluate_DeduplicatesRepeatedStepMentions()
    {
        var evaluator = new Runbook502Evaluator();

        var answer = string.Join(" ",
            "Inspect logs.",
            "Review logs again.",
            "Examine logs one more time.");
        var context = "locating the most recent log entries";
        var input = BuildInput(Runbook502Evaluator.RunbookScenarioId, answer, context);

        var result = evaluator.Evaluate(input);
        var runbookResult = Assert.IsType<Runbook502ScenarioResult>(result.ScenarioResult);

        Assert.Single(runbookResult.PresentStepLabels);
        Assert.DoesNotContain(Runbook502RuleTables.StepInspectLogs, runbookResult.MissingStepLabels);
        Assert.Equal(1, 4 - runbookResult.MissingStepLabels.Count);
    }

    [Fact]
    public void Evaluate_ProducesNoSuggestion_ForStepAlreadyPresent()
    {
        var evaluator = new Runbook502Evaluator();

        var answer = string.Join("\n",
            "- Check upstream dependency health.",
            "- Inspect logs for errors.",
            "- Inspect recent deploy history.",
            "- Rollback to previous version.");
        var context = string.Join(" ",
            "verify that all dependent services are operating correctly",
            "locating the most recent log entries",
            "inspect the deployment history",
            "rollback to the previous stable version");
        var input = BuildInput(Runbook502Evaluator.RunbookScenarioId, answer, context);

        var result = evaluator.Evaluate(input);

        Assert.Empty(result.QuerySuggestions);
    }

    [Fact]
    public void Evaluate_DoesNotThrow_WhenScenarioIdIsRunbook502()
    {
        var evaluator = new Runbook502Evaluator();
        var input = BuildInput(Runbook502Evaluator.RunbookScenarioId, "No content.", "No anchors.");

        var exception = Record.Exception(() => evaluator.Evaluate(input));

        Assert.Null(exception);
    }

    [Fact]
    public void Evaluate_ScoresNinety_WhenAllFourStepsDetectedWithListFormat()
    {
        var evaluator = new Runbook502Evaluator();

        var answer = string.Join("\n",
            "- Check upstream dependency health and verify dependent services are operating.",
            "- Inspect logs for errors and review recent log entries.",
            "- Inspect recent deploy history to identify the cause.",
            "- Rollback to previous stable version if deployment is faulty.");
        var context = string.Join(" ",
            "verify that all dependent services are operating correctly",
            "check the health status of each dependency",
            "locating the most recent log entries",
            "inspect the deployment history",
            "rollback to the previous stable version");
        var input = BuildInput(Runbook502Evaluator.RunbookScenarioId, answer, context);

        var result = evaluator.Evaluate(input);
        var runbookResult = Assert.IsType<Runbook502ScenarioResult>(result.ScenarioResult);

        Assert.Equal(4, runbookResult.PresentStepLabels.Count);
        Assert.Empty(runbookResult.MissingStepLabels);
        Assert.Equal(0, runbookResult.ScoreBreakdown.HallucinationPenalty);
        Assert.Equal(90, result.ScoreTotal);
        Assert.Empty(result.QuerySuggestions);
    }

    private static EvaluationInput BuildInput(string scenarioId, string answer, string context)
    {
        return new EvaluationInput(
            "run-502",
            scenarioId,
            answer,
            new List<SelectedChunk>
            {
                new("doc-01", "doc-01-000", 0, context)
            });
    }
}
