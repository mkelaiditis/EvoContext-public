using System.Globalization;
using EvoContext.Demo;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Rendering;

public sealed class ConsoleRunRendererTests
{
    [Fact]
    public void Baseline_DemoRunRenderer_LivesInDemoNamespace()
    {
        Assert.Equal("EvoContext.Demo", typeof(DemoRunRenderer).Namespace);
    }

    [Fact]
    public void OnEvent_RetrievalCompleted_RendersQueryAndCandidateCount()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.RetrievalCompleted,
            "run-1",
            "policy_refund_v1",
            1,
            new Dictionary<string, object?>
            {
                ["retrieval_query_count"] = 3,
                ["retrieved_count"] = 21,
                ["query_text"] = "What is the refund policy for annual subscriptions?"
            }));

        AssertContainsMessage(sink, "Retrieval completed: query_count=3 candidates=21");
        AssertContainsMessage(sink, "Query:");
    }

    [Fact]
    public void OnEvent_ContextSelected_RendersChunkIdsAndContextChars()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.ContextSelected,
            "run-1",
            "policy_refund_v1",
            2,
            new Dictionary<string, object?>
            {
                ["selected_count"] = 3,
                ["context_character_count"] = 2099,
                ["selected"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["document_id"] = "01",
                        ["chunk_id"] = "01_0",
                        ["chunk_index"] = 0
                    },
                    new()
                    {
                        ["document_id"] = "02",
                        ["chunk_id"] = "02_1",
                        ["chunk_index"] = 1
                    }
                }
            }));

        AssertContainsMessage(sink, "Context selected: chunks=3 context_chars=2099");
        AssertContainsMessage(sink, "Selected chunk ids:");
        AssertContainsMessage(sink, "01:01_0:0");
    }

    [Fact]
    public void OnEvent_GenerationCompleted_RendersAnswerText()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(CreateGenerationCompletedEvent());

        AssertContainsMessage(sink, "Answer:");
        AssertContainsMessage(sink, "A. Summary");
    }

    [Fact]
    public void OnEvent_EvaluationCompleted_RendersScoreAndMissingItems()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(CreateEvaluationCompletedEvent());

        AssertContainsMessage(sink, "Evaluation completed: score_total=60");
        AssertContainsMessage(sink, "Missing items:");
        AssertContainsMessage(sink, "MISSING_COOLING_OFF_WINDOW");
    }

    [Fact]
    public void OnRunComplete_RendersAllRunSummaryFields()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnRunComplete(new RunSummary(
            "run-1",
            "policy_refund_v1",
            RunMode.Run2FeedbackExpanded,
            60,
            70,
            10,
            2));

        AssertContainsMessage(sink, "Run summary:");
        Assert.Contains(sink.Messages, message => message.Contains("run_id", StringComparison.Ordinal) && message.Contains("run-1", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("scenario_id", StringComparison.Ordinal) && message.Contains("policy_refund_v1", StringComparison.Ordinal));
        AssertContainsMessage(sink, "Run2FeedbackExpanded");
        AssertContainsMessage(sink, "score_run1=60");
        AssertContainsMessage(sink, "score_run2=70");
        AssertContainsMessage(sink, "score_delta=10");
        AssertContainsMessage(sink, "memory_updates=2");
    }

    [Fact]
    public void OnRunComplete_RendersRun1AndRun2AnswerSectionsInDemoSummary()
    {
        var renderer = CreateRenderer(out var sink);

        RenderCompletedPolicySummary(
            renderer,
            run1Answer: "Run 1 answer summary.",
            run2Answer: "Run 2 answer summary with more detail.");

        AssertContainsMessage(sink, "RUN 1 RESULT");
        AssertContainsMessage(sink, "Run 1 answer:");
        AssertContainsMessage(sink, "Run 1 answer summary.");
        AssertContainsMessage(sink, "RUN 2 RESULT");
        AssertContainsMessage(sink, "Run 2 answer:");
        AssertContainsMessage(sink, "Run 2 answer summary with more detail.");
        AssertContainsMessage(sink, "Score improvement: +10");
    }

    [Fact]
    public void OnRunComplete_RendersOnlyRun1AnswerSection_WhenRun2IsAbsent()
    {
        var renderer = CreateRenderer(out var sink);

        RenderCompletedPolicySummary(
            renderer,
            run1Answer: "Run 1 answer summary.",
            run2Answer: null,
            run2Score: null);

        AssertContainsMessage(sink, "RUN 1 RESULT");
        AssertContainsMessage(sink, "Run 1 answer:");
        Assert.DoesNotContain(sink.Messages, message => message.Contains("RUN 2 RESULT", StringComparison.Ordinal));
        Assert.DoesNotContain(sink.Messages, message => message.Contains("Run 2 answer:", StringComparison.Ordinal));
        Assert.DoesNotContain(sink.Messages, message => message.Contains("Score improvement:", StringComparison.Ordinal));
    }

    [Fact]
    public void OnRunComplete_OmitsRunAnswerSection_WhenAnswerIsEmpty()
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

    [Fact]
    public void OnRunComplete_WrapsAnswerTextToSixtyCharactersPerLine()
    {
        var renderer = CreateRenderer(out var sink);
        var longAnswer = string.Join(" ", Enumerable.Repeat("word", 13));
        var expectedFirstLine = "    " + string.Join(" ", Enumerable.Repeat("word", 12));
        const string expectedSecondLine = "    word";

        RenderCompletedPolicySummary(
            renderer,
            run1Answer: longAnswer,
            run2Answer: null,
            run2Score: null);

        Assert.Contains(sink.Messages, message => string.Equals(message, expectedFirstLine, StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => string.Equals(message, expectedSecondLine, StringComparison.Ordinal));
    }

    [Fact]
    public void OnRunComplete_RendersPresentAndMissingBadgeRows_WhenPresentMetadataExists()
    {
        var renderer = CreateRenderer(out var sink);

        RenderCompletedPolicySummary(
            renderer,
            run1Answer: "Run 1 answer summary.",
            run1PresentFactLabels: new[]
            {
                Phase4RuleTables.PresentCoolingOffWindow,
                Phase4RuleTables.PresentAnnualProrationRule
            },
            run1MissingFactLabels: new[]
            {
                Phase4RuleTables.MissingBillingErrorException
            },
            run2Answer: null,
            run2Score: null);

        AssertContainsMessage(sink, "Present:  14-day cooling-off window, Annual subscription proration rule");
        AssertContainsMessage(sink, "Missing:  Billing error refund exception");
    }

    [Fact]
    public void OnRunComplete_OmitsBadgeRows_WhenPresentMetadataIsAbsent()
    {
        var renderer = CreateRenderer(out var sink);

        RenderCompletedPolicySummary(
            renderer,
            run1Answer: "Run 1 answer summary.",
            run1MissingFactLabels: new[]
            {
                Phase4RuleTables.MissingBillingErrorException
            },
            run2Answer: null,
            run2Score: null);

        Assert.DoesNotContain(sink.Messages, message => message.Contains("Present:", StringComparison.Ordinal));
        Assert.DoesNotContain(sink.Messages, message => message.Contains("Missing:  Billing error refund exception", StringComparison.Ordinal));
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
        IReadOnlyList<string>? run1PresentFactLabels = null,
        IReadOnlyList<string>? run1MissingFactLabels = null,
        int run1Score = 60,
        string? run2Answer = "Run 2 answer",
        IReadOnlyList<string>? run2PresentFactLabels = null,
        IReadOnlyList<string>? run2MissingFactLabels = null,
        IReadOnlyList<string>? run2OrderViolationLabels = null,
        int? run2Score = 70,
        int memoryUpdatesCount = 2)
    {
        renderer.OnEvent(CreateGenerationCompletedEvent(
            runId: runId,
            scenarioId: scenarioId,
            answer: run1Answer));
        renderer.OnEvent(CreateEvaluationCompletedEvent(
            runId: runId,
            scenarioId: scenarioId,
            scoreTotal: run1Score,
            runMode: RunMode.Run1AnswerGeneration,
            presentFactLabels: run1PresentFactLabels,
            missingFactLabels: run1MissingFactLabels));

        if (run2Answer is not null || run2Score.HasValue)
        {
            renderer.OnEvent(CreateGenerationCompletedEvent(
                runId: runId,
                scenarioId: scenarioId,
                answer: run2Answer ?? string.Empty));
            renderer.OnEvent(CreateEvaluationCompletedEvent(
                runId: runId,
                scenarioId: scenarioId,
                scoreTotal: run2Score ?? 0,
                runMode: RunMode.Run2FeedbackExpanded,
                presentFactLabels: run2PresentFactLabels,
                missingFactLabels: run2MissingFactLabels,
                orderViolationLabels: run2OrderViolationLabels));
        }

        renderer.OnRunComplete(CreateRunSummary(
            runId: runId,
            scenarioId: scenarioId,
            run1Score: run1Score,
            run2Score: run2Score,
            memoryUpdatesCount: memoryUpdatesCount));
    }

    private static RunSummary CreateRunSummary(
        string runId = "run-1",
        string scenarioId = "policy_refund_v1",
        int run1Score = 60,
        int? run2Score = 70,
        int memoryUpdatesCount = 2)
    {
        return new RunSummary(
            runId,
            scenarioId,
            run2Score.HasValue ? RunMode.Run2FeedbackExpanded : RunMode.Run1AnswerGeneration,
            run1Score,
            run2Score,
            run2Score.HasValue ? run2Score.Value - run1Score : null,
            memoryUpdatesCount);
    }

    private static TraceEvent CreateGenerationCompletedEvent(
        string runId = "run-1",
        string scenarioId = "policy_refund_v1",
        string answer = "A. Summary\nanswer")
    {
        return new TraceEvent(
            TraceEventType.GenerationCompleted,
            runId,
            scenarioId,
            3,
            new Dictionary<string, object?>
            {
                ["raw_model_output"] = answer
            });
    }

    private static TraceEvent CreateEvaluationCompletedEvent(
        string runId = "run-1",
        string scenarioId = "policy_refund_v1",
        int scoreTotal = 60,
        RunMode? runMode = null,
        IReadOnlyList<string>? missingItems = null,
        IReadOnlyList<string>? presentFactLabels = null,
        IReadOnlyList<string>? presentStepLabels = null,
        IReadOnlyList<string>? missingFactLabels = null,
        IReadOnlyList<string>? missingStepLabels = null,
        IReadOnlyList<string>? orderViolationLabels = null)
    {
        var effectiveMissingFactLabels = missingFactLabels ??
            (missingStepLabels is null && missingItems is null
                ? new[] { "MISSING_COOLING_OFF_WINDOW" }
                : null);
        var effectiveMissingItems = missingItems
            ?? effectiveMissingFactLabels
            ?? missingStepLabels
            ?? Array.Empty<string>();
        var metadata = new Dictionary<string, object?>
        {
            ["score_total"] = scoreTotal,
            ["missing_items"] = effectiveMissingItems
        };

        if (runMode.HasValue)
        {
            metadata["run_mode"] = runMode.Value.ToString();
        }

        if (effectiveMissingFactLabels is not null)
        {
            metadata["missing_fact_labels"] = effectiveMissingFactLabels;
        }

        if (presentFactLabels is not null)
        {
            metadata["present_fact_labels"] = presentFactLabels;
        }

        if (presentStepLabels is not null)
        {
            metadata["present_step_labels"] = presentStepLabels;
        }

        if (missingStepLabels is not null)
        {
            metadata["missing_step_labels"] = missingStepLabels;
        }

        if (orderViolationLabels is not null)
        {
            metadata["order_violation_labels"] = orderViolationLabels;
        }

        return new TraceEvent(
            TraceEventType.EvaluationCompleted,
            runId,
            scenarioId,
            4,
            metadata);
    }

    private static void AssertContainsMessage(CollectingSink sink, string expected)
    {
        Assert.Contains(sink.Messages, message => message.Contains(expected, StringComparison.Ordinal));
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<string> Messages { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            Messages.Add(logEvent.RenderMessage(CultureInfo.InvariantCulture));
        }
    }
}
