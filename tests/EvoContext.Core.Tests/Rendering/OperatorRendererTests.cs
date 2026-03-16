using EvoContext.Cli;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Rendering;

public sealed class OperatorRendererTests
{
    [Fact]
    public void OnEvent_RunStarted_RendersStructuredFields()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.RunStarted,
            "run-1",
            "policy_refund_v1",
            1,
            new Dictionary<string, object?>
            {
                ["run_mode"] = RunMode.Run1AnswerGeneration.ToString()
            }));

        AssertContainsMessage(sink, "event=RunStarted");
        AssertContainsMessage(sink, "run_id=");
        AssertContainsMessage(sink, "scenario_id=");
        Assert.Contains(
            sink.Messages,
            message => message.Contains("run_mode=", StringComparison.Ordinal)
                && message.Contains("Run1AnswerGeneration", StringComparison.Ordinal));
    }

    [Fact]
    public void OnEvent_RetrievalCompleted_RendersStructuredFieldsWithoutNarration()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.RetrievalCompleted,
            "run-1",
            "policy_refund_v1",
            2,
            new Dictionary<string, object?>
            {
                ["retrieval_query_count"] = 3,
                ["retrieved_count"] = 21
            }));

        AssertContainsMessage(sink, "event=RetrievalCompleted");
        AssertContainsMessage(sink, "retrieval_query_count=3");
        AssertContainsMessage(sink, "retrieved_count=21");
        AssertDoesNotContainMessage(sink, "cooling-off window");
    }

    [Fact]
    public void OnEvent_ContextSelected_RendersStructuredFields()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.ContextSelected,
            "run-1",
            "policy_refund_v1",
            3,
            new Dictionary<string, object?>
            {
                ["selected_count"] = 3,
                ["context_character_count"] = 2099
            }));

        AssertContainsMessage(sink, "event=ContextSelected");
        AssertContainsMessage(sink, "selected_count=3");
        AssertContainsMessage(sink, "context_character_count=2099");
    }

    [Fact]
    public void OnEvent_GenerationCompleted_OnlyRendersAnswerLength()
    {
        var renderer = CreateRenderer(out var sink);
        const string answer = "A. Summary\nSensitive answer details";

        renderer.OnEvent(new TraceEvent(
            TraceEventType.GenerationCompleted,
            "run-1",
            "policy_refund_v1",
            4,
            new Dictionary<string, object?>
            {
                ["raw_model_output"] = answer
            }));

        AssertContainsMessage(sink, "event=GenerationCompleted");
        AssertContainsMessage(sink, $"answer_length={answer.Length}");
        AssertDoesNotContainMessage(sink, "Sensitive answer details");
    }

    [Fact]
    public void OnEvent_Run2Triggered_RendersStructuredFields()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.Run2Triggered,
            "run-1",
            "policy_refund_v1",
            7,
            new Dictionary<string, object?>
            {
                ["label"] = "Run 2 triggered — score below threshold",
                ["expanded_query_count"] = 2
            }));

        AssertContainsMessage(sink, "event=Run2Triggered");
        AssertContainsMessage(sink, "expanded_query_count=2");
        AssertContainsMessage(sink, "Run 2 triggered");
    }

    [Fact]
    public void OnEvent_RunFinished_RendersCompletionEvent()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.RunFinished,
            "run-1",
            "policy_refund_v1",
            5,
            new Dictionary<string, object?>()));

        AssertContainsMessage(sink, "event=RunFinished");
        AssertContainsMessage(sink, "run_id=");
        AssertContainsMessage(sink, "scenario_id=");
    }

    [Fact]
    public void OnEvent_RunSummary_RendersStructuredSummaryEventFields()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.RunSummary,
            "run-1",
            "policy_refund_v1",
            8,
            new Dictionary<string, object?>
            {
                ["run_mode"] = RunMode.Run2FeedbackExpanded.ToString(),
                ["score_run1"] = 60,
                ["score_run2"] = 70,
                ["score_delta"] = 10,
                ["memory_updates"] = 2
            }));

        AssertContainsMessage(sink, "event=RunSummaryEvent");
        Assert.Contains(
            sink.Messages,
            message => message.Contains("run_mode=", StringComparison.Ordinal)
                && message.Contains("Run2FeedbackExpanded", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("score_run1=", StringComparison.Ordinal) && message.Contains("60", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("score_run2=", StringComparison.Ordinal) && message.Contains("70", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("score_delta=", StringComparison.Ordinal) && message.Contains("10", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("memory_updates=", StringComparison.Ordinal) && message.Contains("2", StringComparison.Ordinal));
    }

    [Fact]
    public void OnEvent_DefaultBranch_RendersUnknownEventTypeTag()
    {
        var renderer = CreateRenderer(out var sink);

        renderer.OnEvent(new TraceEvent(
            TraceEventType.EmbeddingIngestCompleted,
            "run-embed-1",
            "policy_refund_v1",
            1,
            new Dictionary<string, object?>()));

        AssertContainsMessage(sink, "event=EmbeddingIngestCompleted");
        AssertContainsMessage(sink, "run_id=");
        AssertContainsMessage(sink, "scenario_id=");
    }

    [Fact]
    public void OnEvent_EvaluationCompleted_PreservesMissingItemCodes()
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
                ["missing_fact_labels"] = new[] { "MISSING_COOLING_OFF_WINDOW" },
                ["run_mode"] = RunMode.Run1AnswerGeneration.ToString()
            }));

        AssertContainsMessage(sink, "event=EvaluationCompleted");
        Assert.Contains(
            sink.Messages,
            message => message.Contains("missing_fact_labels=", StringComparison.Ordinal)
                && message.Contains("MISSING_COOLING_OFF_WINDOW", StringComparison.Ordinal));
        Assert.DoesNotContain(
            sink.Messages,
            message => message.Contains("14-day cooling-off window", StringComparison.Ordinal));
    }

    [Fact]
    public void OnRunComplete_RendersStructuredSummaryFields()
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

        AssertContainsMessage(sink, "event=RunSummary");
        Assert.Contains(sink.Messages, message => message.Contains("run_id=", StringComparison.Ordinal) && message.Contains("run-1", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("scenario_id=", StringComparison.Ordinal) && message.Contains("policy_refund_v1", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("score_run1=60", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("score_run2=70", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("score_delta=10", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("memory_updates=2", StringComparison.Ordinal));
    }

    private static OperatorRenderer CreateRenderer(out CollectingSink sink)
    {
        sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(sink)
            .CreateLogger();

        return new OperatorRenderer(logger);
    }

    private static void AssertContainsMessage(CollectingSink sink, string expectedFragment)
    {
        Assert.Contains(
            sink.Messages,
            message => message.Contains(expectedFragment, StringComparison.Ordinal));
    }

    private static void AssertDoesNotContainMessage(CollectingSink sink, string disallowedFragment)
    {
        Assert.DoesNotContain(
            sink.Messages,
            message => message.Contains(disallowedFragment, StringComparison.Ordinal));
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
