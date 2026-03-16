using EvoContext.Core.Evaluation;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class EvaluationDebugLoggingTests
{
    [Fact]
    public void Evaluate_EmitsStructuredPhase4DebugLogs()
    {
        var logger = CreateLogger(out var sink);
        var dispatcher = new ScenarioEvaluatorDispatcher(
            new IScenarioEvaluator[]
            {
                new PolicyRefundEvaluator(new Phase4Evaluator(logger: logger))
            },
            logger);

        var result = dispatcher.Evaluate(BuildPolicyInput());
        var policyResult = Assert.IsType<PolicyRefundScenarioResult>(result.ScenarioResult);

        Assert.Equal(80, result.ScoreTotal);

        var selectionEvent = AssertSingleEvent(sink, "Evaluator selected");
        Assert.Equal(PolicyRefundEvaluator.PolicyScenarioId, GetScalarString(selectionEvent, "scenario_id"));
        Assert.Equal(nameof(PolicyRefundEvaluator), GetScalarString(selectionEvent, "evaluator_type"));

        var startEvent = AssertSingleEvent(sink, "Phase 4 evaluation started");
        Assert.Equal(PolicyRefundEvaluator.PolicyScenarioId, GetScalarString(startEvent, "scenario_id"));
        Assert.True(GetScalarInt(startEvent, "answer_length") > 0);
        Assert.True(GetScalarInt(startEvent, "context_length") > 0);

        Assert.Contains(
            sink.Events,
            evt => evt.MessageTemplate.Text == "Phase 4 fact evaluated"
                && GetScalarString(evt, "fact_id") == "F1"
                && GetScalarBoolean(evt, "is_present"));

        Assert.Contains(
            sink.Events,
            evt => evt.MessageTemplate.Text == "Phase 4 fact evaluated"
                && GetScalarString(evt, "fact_id") == "F3"
                && !GetScalarBoolean(evt, "is_present"));

        var hallucinationEvent = AssertSingleEvent(sink, "Phase 4 hallucination evaluated");
        Assert.Equal(0, GetScalarInt(hallucinationEvent, "flag_count"));
        Assert.Equal(0, GetScalarInt(hallucinationEvent, "hallucination_penalty"));

        var scoreEvent = AssertSingleEvent(sink, "Phase 4 score computed");
        Assert.Equal(policyResult.ScoreBreakdown.CompletenessPoints, GetScalarInt(scoreEvent, "completeness_points"));
        Assert.Equal(policyResult.ScoreBreakdown.FormatPoints, GetScalarInt(scoreEvent, "format_points"));
        Assert.Equal(result.ScoreTotal, GetScalarInt(scoreEvent, "score_total"));
    }

    [Fact]
    public void Evaluate_EmitsStructuredRunbook502DebugLogs()
    {
        var logger = CreateLogger(out var sink);
        var dispatcher = new ScenarioEvaluatorDispatcher(
            new IScenarioEvaluator[]
            {
                new Runbook502Evaluator(logger: logger)
            },
            logger);

        var result = dispatcher.Evaluate(BuildRunbookInput());

        Assert.True(result.ScoreTotal >= 0);

        var selectionEvent = AssertSingleEvent(sink, "Evaluator selected");
        Assert.Equal(Runbook502Evaluator.RunbookScenarioId, GetScalarString(selectionEvent, "scenario_id"));
        Assert.Equal(nameof(Runbook502Evaluator), GetScalarString(selectionEvent, "evaluator_type"));

        Assert.Contains(
            sink.Events,
            evt => evt.MessageTemplate.Text == "Runbook 502 step evaluated"
                && GetScalarString(evt, "step_label") == Runbook502RuleTables.StepInspectLogs
                && GetScalarBoolean(evt, "is_present"));

        var stepSummaryEvent = AssertSingleEvent(sink, "Runbook 502 step evaluation completed");
        Assert.Equal(2, GetScalarInt(stepSummaryEvent, "order_violation_count"));

        var hallucinationEvent = AssertSingleEvent(sink, "Runbook 502 hallucination evaluated");
        Assert.Equal(0, GetScalarInt(hallucinationEvent, "flag_count"));

        var scoreEvent = AssertSingleEvent(sink, "Runbook 502 score computed");
        Assert.True(GetScalarInt(scoreEvent, "step_coverage_points") > 0);
        Assert.Equal(result.ScoreTotal, GetScalarInt(scoreEvent, "score_total"));
    }

    private static EvaluationInput BuildPolicyInput()
    {
        return new EvaluationInput(
            "run-001",
            PolicyRefundEvaluator.PolicyScenarioId,
            BuildPolicyAnswer(),
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

    private static string BuildPolicyAnswer()
    {
        var filler = string.Join(" ", Enumerable.Repeat("word", 150));
        var summary = $"A. Summary\n{filler} within 14 days cooling-off.";
        var eligibility = "B. Eligibility Rules\nEligibility covers prorated refund for unused month early termination.";
        var exceptions = "C. Exceptions\nno explicit exception coverage.";
        var timeline = "D. Timeline and Process\nrefunds are handled after cancel via portal with account ID.";

        return string.Join("\n\n", summary, eligibility, exceptions, timeline);
    }

    private static EvaluationInput BuildRunbookInput()
    {
        return new EvaluationInput(
            "run-502",
            Runbook502Evaluator.RunbookScenarioId,
            string.Join("\n",
                "- Rollback to previous stable version.",
                "- Check upstream dependency health.",
                "- Inspect logs for startup failures."),
            new List<SelectedChunk>
            {
                new(
                    "doc-01",
                    "doc-01-000",
                    0,
                    string.Join(
                        " ",
                        "verify that all dependent services are operating correctly",
                        "locating the most recent log entries",
                        "rollback to the previous stable version"))
            });
    }

    private static ILogger CreateLogger(out CollectingSink sink)
    {
        sink = new CollectingSink();
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();
    }

    private static LogEvent AssertSingleEvent(CollectingSink sink, string template)
    {
        return Assert.Single(sink.Events, evt => evt.MessageTemplate.Text == template);
    }

    private static string GetScalarString(LogEvent logEvent, string propertyName)
    {
        var scalar = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        return Assert.IsType<string>(scalar.Value);
    }

    private static int GetScalarInt(LogEvent logEvent, string propertyName)
    {
        var scalar = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        return Convert.ToInt32(scalar.Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool GetScalarBoolean(LogEvent logEvent, string propertyName)
    {
        var scalar = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        return Assert.IsType<bool>(scalar.Value);
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}