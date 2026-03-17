using EvoContext.Cli.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Execution;

public sealed class CliRun2MatchedQueryRenderingTests
{
    [Fact]
    public void WriteSummary_RendersMatchedQuery_ForRun2Candidates_WhenQueryTextExists()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var renderer = new RetrievalSummaryRenderer();
        var result = RetrievalRenderingFixtures.CreateRunResult(new[]
        {
            RetrievalRenderingFixtures.CreateCandidate(
                rank: 1,
                documentId: "01",
                chunkId: "01_0",
                chunkIndex: 0,
                similarity: 0.95f,
                queryIdentifier: "run2_q3",
                queryText: "service returns 502")
        });

        renderer.WriteSummary(logger, result, run: 1, repeat: 1, includeAnswer: false);

        Assert.Contains(sink.Messages, message => message.Contains("Matched query: service returns 502", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteSummary_RendersSourceFallback_ForRun2Candidates_WhenQueryTextMissing()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var renderer = new RetrievalSummaryRenderer();
        var result = RetrievalRenderingFixtures.CreateRunResult(new[]
        {
            RetrievalRenderingFixtures.CreateCandidate(
                rank: 1,
                documentId: "02",
                chunkId: "02_0",
                chunkIndex: 0,
                similarity: 0.84f,
                queryIdentifier: "run2_q2",
                queryText: null),
            RetrievalRenderingFixtures.CreateCandidate(
                rank: 2,
                documentId: "03",
                chunkId: "03_1",
                chunkIndex: 1,
                similarity: 0.80f,
                queryIdentifier: "run2_q1",
                queryText: null)
        });

        renderer.WriteSummary(logger, result, run: 1, repeat: 1, includeAnswer: false);

        Assert.Contains(sink.Messages, message => message.Contains("Query source: feedback expansion", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("Query source: base query", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteSummary_DoesNotRenderRun2Attribution_ForRun1Candidates()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var renderer = new RetrievalSummaryRenderer();
        var result = RetrievalRenderingFixtures.CreateRunResult(new[]
        {
            RetrievalRenderingFixtures.CreateCandidate(
                rank: 1,
                documentId: "04",
                chunkId: "04_0",
                chunkIndex: 0,
                similarity: 0.73f,
                queryIdentifier: "run1_primary",
                queryText: "refund policy")
        });

        renderer.WriteSummary(logger, result, run: 1, repeat: 1, includeAnswer: false);

        Assert.DoesNotContain(sink.Messages, message => message.Contains("Matched query:", StringComparison.Ordinal));
        Assert.DoesNotContain(sink.Messages, message => message.Contains("Query source:", StringComparison.Ordinal));
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<string> Messages { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            Messages.Add(logEvent.RenderMessage());
        }
    }
}
