using EvoContext.Core.Evaluation;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Demo;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Rendering;

public sealed class DemoRunRendererTests
{
    [Fact]
    public void OnEvent_GenerationCompleted_RendersAnswerText()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.GenerationCompleted,
            "run-1",
            "policy_refund_v1",
            3,
            new Dictionary<string, object?>
            {
                ["raw_model_output"] = "A. Summary\nanswer"
            }));

        AssertContainsMessage(sink, "Answer:");
        AssertContainsMessage(sink, "A. Summary");
    }

    [Fact]
    public void OnEvent_EvaluationCompleted_RendersMissingItemCodes()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.EvaluationCompleted,
            "run-1",
            "policy_refund_v1",
            4,
            new Dictionary<string, object?>
            {
                ["score_total"] = 60,
                ["missing_items"] = new[] { Phase4RuleTables.MissingCoolingOffWindow },
                ["missing_fact_labels"] = new[] { Phase4RuleTables.MissingCoolingOffWindow },
                ["run_mode"] = RunMode.Run1AnswerGeneration.ToString()
            }));

        AssertContainsMessage(sink, "Evaluation completed: score_total=60");
        AssertContainsMessage(sink, "MISSING_COOLING_OFF_WINDOW");
    }

    [Fact]
    public void OnRunComplete_RendersRichDemoSummary()
    {
        var renderer = CreateRenderer(out var sink);

        RenderCompletedPolicySummary(
            renderer,
            run1Answer: "Run 1 answer summary.",
            run2Answer: "Run 2 answer summary with more detail.");

        AssertContainsMessage(sink, "RUN 1 RESULT");
        AssertContainsMessage(sink, "Run 1 answer:");
        AssertContainsMessage(sink, "RUN 2 RESULT");
        AssertContainsMessage(sink, "Run 2 answer:");
        AssertContainsMessage(sink, "Score improvement: +10");
    }

    [Fact]
    public void OnRunComplete_OmitsAnswerSection_WhenAnswerMissing()
    {
        var renderer = CreateRenderer(out var sink);

        RenderCompletedPolicySummary(
            renderer,
            run1Answer: "Run 1 answer summary.",
            run2Answer: string.Empty,
            run2Score: 70);

        AssertContainsMessage(sink, "RUN 2 RESULT");
        Assert.DoesNotContain(sink.Messages, message => message.Contains("Run 2 answer:", StringComparison.Ordinal));
    }

    private static DemoRunRenderer CreateRenderer(out CollectingSink sink)
    {
        sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(sink)
            .CreateLogger();

        return new DemoRunRenderer(logger);
    }

    private static void RenderCompletedPolicySummary(
        DemoRunRenderer renderer,
        string runId = "run-1",
        string scenarioId = "policy_refund_v1",
        string run1Answer = "Run 1 answer",
        int run1Score = 60,
        string? run2Answer = "Run 2 answer",
        int? run2Score = 70,
        int memoryUpdatesCount = 2)
    {
        renderer.OnEvent(new TraceEvent(
            TraceEventType.GenerationCompleted,
            runId,
            scenarioId,
            3,
            new Dictionary<string, object?>
            {
                ["raw_model_output"] = run1Answer
            }));

        renderer.OnEvent(new TraceEvent(
            TraceEventType.EvaluationCompleted,
            runId,
            scenarioId,
            4,
            new Dictionary<string, object?>
            {
                ["score_total"] = run1Score,
                ["run_mode"] = RunMode.Run1AnswerGeneration.ToString(),
                ["missing_fact_labels"] = new[] { Phase4RuleTables.MissingCoolingOffWindow }
            }));

        if (run2Answer is not null || run2Score.HasValue)
        {
            renderer.OnEvent(new TraceEvent(
                TraceEventType.GenerationCompleted,
                runId,
                scenarioId,
                5,
                new Dictionary<string, object?>
                {
                    ["raw_model_output"] = run2Answer ?? string.Empty
                }));

            renderer.OnEvent(new TraceEvent(
                TraceEventType.EvaluationCompleted,
                runId,
                scenarioId,
                6,
                new Dictionary<string, object?>
                {
                    ["score_total"] = run2Score ?? 0,
                    ["run_mode"] = RunMode.Run2FeedbackExpanded.ToString(),
                    ["missing_fact_labels"] = Array.Empty<string>()
                }));
        }

        renderer.OnRunComplete(new RunSummary(
            runId,
            scenarioId,
            run2Score.HasValue ? RunMode.Run2FeedbackExpanded : RunMode.Run1AnswerGeneration,
            run1Score,
            run2Score,
            run2Score.HasValue ? run2Score.Value - run1Score : null,
            memoryUpdatesCount));
    }

    private static void AssertContainsMessage(CollectingSink sink, string expectedFragment)
    {
        Assert.Contains(
            sink.Messages,
            message => message.Contains(expectedFragment, StringComparison.Ordinal));
    }

    private sealed class CollectingSink : ILogEventSink
    {
        private readonly List<string> _messages = new();

        public IReadOnlyList<string> Messages => _messages;

        public void Emit(LogEvent logEvent)
        {
            _messages.Add(logEvent.RenderMessage());
        }
    }
}
