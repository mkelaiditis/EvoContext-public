using EvoContext.Core.Evaluation;
using EvoContext.Core.Tracing;
using EvoContext.Cli;
using EvoContext.Demo;
using EvoContext.Infrastructure.Models;
using EvoContext.Infrastructure.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Replay;

public sealed class ReplayRendererTests
{
    [Fact]
    public void Render_WithOperatorRenderer_PreservesCodeLabels()
    {
        var artifact = CreatePolicyRun2Artifact();
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var renderer = new OperatorRenderer(logger);
        var replayRenderer = new ReplayRenderer();
        replayRenderer.Render(renderer, artifact);

        Assert.Contains(sink.Messages, message => message.Contains("missing_items=", StringComparison.Ordinal) && message.Contains("MISSING_COOLING_OFF_WINDOW", StringComparison.Ordinal));
        Assert.DoesNotContain(
            sink.Messages,
            message => message.Contains("14-day cooling-off window", StringComparison.Ordinal));
    }

    [Fact]
    public void Baseline_DemoRunRenderer_LivesInDemoNamespace()
    {
        Assert.Equal("EvoContext.Demo", typeof(DemoRunRenderer).Namespace);
    }

    [Fact]
    public void Render_EmitsReplayEventsAndFinalSummary_FromTraceArtifact()
    {
        var artifact = CreatePolicyRun2Artifact();

        var recordingRenderer = RenderArtifact(artifact);

        Assert.Collection(
            recordingRenderer.Events,
            first => Assert.Equal(TraceEventType.RetrievalCompleted, first.EventType),
            second => Assert.Equal(TraceEventType.ContextSelected, second.EventType),
            third => Assert.Equal(TraceEventType.GenerationCompleted, third.EventType),
            fourth => Assert.Equal(TraceEventType.EvaluationCompleted, fourth.EventType),
            fifth => Assert.Equal(TraceEventType.Run2Triggered, fifth.EventType),
            sixth => Assert.Equal(TraceEventType.RunFinished, sixth.EventType));

        var retrieval = GetSingleEvent(recordingRenderer.Events, TraceEventType.RetrievalCompleted);
        Assert.Equal(2, Assert.IsType<int>(retrieval.Metadata["retrieval_query_count"]!));
        Assert.Equal("What is the refund policy for annual subscriptions?", retrieval.Metadata["query_text"]);

        var context = GetSingleEvent(recordingRenderer.Events, TraceEventType.ContextSelected);
        Assert.Equal(2, Assert.IsType<int>(context.Metadata["selected_count"]!));

        var evaluation = GetSingleEvent(recordingRenderer.Events, TraceEventType.EvaluationCompleted);
        Assert.Equal(70, Assert.IsType<int>(evaluation.Metadata["score_total"]!));
        var presentFacts = Assert.IsAssignableFrom<IReadOnlyList<string>>(evaluation.Metadata["present_fact_labels"]!);
        Assert.Contains(Phase4RuleTables.PresentCoolingOffWindow, presentFacts);
        var missingItems = Assert.IsAssignableFrom<IReadOnlyList<string>>(evaluation.Metadata["missing_items"]!);
        Assert.Contains("MISSING_COOLING_OFF_WINDOW", missingItems);

        var run2Triggered = GetSingleEvent(recordingRenderer.Events, TraceEventType.Run2Triggered);
        Assert.Equal("Run 2 triggered — score below threshold", run2Triggered.Metadata["label"]);
        Assert.Equal(2, Assert.IsType<int>(run2Triggered.Metadata["expanded_query_count"]!));

        AssertSummaryMatchesArtifact(artifact, Assert.Single(recordingRenderer.Summaries));
    }

    private static TraceArtifact CreatePolicyRun2Artifact()
    {
        return CreatePolicyArtifact(runMode: "run2", scoreRun2: 70, scoreDelta: 10);
    }

    private static TraceArtifact CreatePolicyArtifact(
        string runMode = "run2",
        int scoreTotal = 70,
        int scoreRun1 = 60,
        int? scoreRun2 = 70,
        int? scoreDelta = 10)
    {
        return new TraceArtifact(
            "policy_refund_v1_20260310T120000Z_abcd",
            "policy_refund_v1",
            "policy_refund_v1",
            "What is the refund policy for annual subscriptions?",
            runMode,
            "2026-03-10T12:00:00Z",
            new[]
            {
                "base query",
                "feedback query"
            },
            21,
            new[]
            {
                new TraceArtifactSelectedChunk("01", "01_0", 0, "chunk one"),
                new TraceArtifactSelectedChunk("02", "02_1", 1, "chunk two")
            },
            2099,
            "A. Summary\nanswer",
            scoreTotal,
            new[]
            {
                "query suggestion"
            },
            scoreRun1,
            scoreRun2,
            scoreDelta,
            runMode.Equals("run2", StringComparison.OrdinalIgnoreCase)
                ? new[] { "02_1" }
                : Array.Empty<string>(),
            new PolicyRefundScenarioResultPayload(
                new[]
                {
                    Phase4RuleTables.PresentCoolingOffWindow,
                    Phase4RuleTables.PresentAnnualProrationRule
                },
                new[]
                {
                    "MISSING_COOLING_OFF_WINDOW"
                },
                Array.Empty<string>(),
                new PolicyRefundScoreBreakdownPayload(40, 20, 0, false)));
    }

    private static RecordingRenderer RenderArtifact(TraceArtifact artifact)
    {
        var recordingRenderer = new RecordingRenderer();
        var replayRenderer = new ReplayRenderer();
        replayRenderer.Render(recordingRenderer, artifact);
        return recordingRenderer;
    }

    private static TraceEvent GetSingleEvent(IEnumerable<TraceEvent> events, TraceEventType eventType)
    {
        return Assert.Single(events, evt => evt.EventType == eventType);
    }

    private static void AssertSummaryMatchesArtifact(TraceArtifact artifact, RunSummary summary)
    {
        Assert.Equal(artifact.RunId, summary.RunId);
        Assert.Equal(artifact.ScenarioId, summary.ScenarioId);
        Assert.Equal(artifact.ScoreRun1, summary.ScoreRun1);
        Assert.Equal(artifact.ScoreRun2, summary.ScoreRun2);
        Assert.Equal(artifact.ScoreDelta, summary.ScoreDelta);
    }

    private sealed class RecordingRenderer : IRunRenderer
    {
        public List<TraceEvent> Events { get; } = new();

        public List<RunSummary> Summaries { get; } = new();

        public void OnEvent(TraceEvent evt)
        {
            Events.Add(evt);
        }

        public void OnRunComplete(RunSummary summary)
        {
            Summaries.Add(summary);
        }
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
