using System.Globalization;
using System.Text.Json;
using EvoContext.Demo;
using EvoContext.Infrastructure.Models;
using EvoContext.Infrastructure.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Replay;

public sealed class ReplayTests
{
    [Fact]
    public void ReplayRendering_FromTraceArtifactFile_RendersRequiredFields()
    {
        using var temp = new TempDirectory();
        var scenarioId = "policy_refund_v1";
        var runId = scenarioId + "_20260310T120000Z_abcd";
        var traceDirectory = Path.Combine(temp.Path, "artifacts", "traces", scenarioId);
        Directory.CreateDirectory(traceDirectory);

        var artifact = new TraceArtifact(
            runId,
            scenarioId,
            scenarioId,
            "What is the refund policy for annual subscriptions?",
            "run2",
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
            70,
            new[]
            {
                "query suggestion"
            },
            60,
            70,
            10,
            new[]
            {
                "02_1"
            },
            new PolicyRefundScenarioResultPayload(
                Array.Empty<string>(),
                new[]
                {
                    "MISSING_COOLING_OFF_WINDOW"
                },
                Array.Empty<string>(),
                new PolicyRefundScoreBreakdownPayload(40, 20, 0, false)));

        var tracePath = Path.Combine(traceDirectory, runId + ".json");
        var json = JsonSerializer.Serialize(artifact, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(tracePath, json);

        var reader = new TraceArtifactReader();
        var loadedArtifact = reader.Read(tracePath);

        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var renderer = new DemoRunRenderer(logger);
        var replayRenderer = new ReplayRenderer();

        replayRenderer.Render(renderer, loadedArtifact);

        Assert.Contains(sink.Messages, message => message.Contains("Retrieval completed: query_count=2", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("Context selected: chunks=2 context_chars=2099", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("Selected chunk ids:", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("01:01_0:0", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("02:02_1:1", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("Answer:", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("A. Summary", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("Evaluation completed: score_total=70", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("Missing items:", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("MISSING_COOLING_OFF_WINDOW", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("Run finished:", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains(runId, StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains(scenarioId, StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("mode=", StringComparison.Ordinal) && message.Contains("run2", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("What is the refund policy for annual subscriptions?", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("score_delta=10", StringComparison.Ordinal));
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
