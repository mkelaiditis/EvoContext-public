using EvoContext.Cli.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Execution;

public sealed class CliSelectedChunksRenderingTests
{
    [Fact]
    public void WriteSummary_RendersSelectedChunks_WithLabelAndTechnicalSuffix()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var renderer = new RetrievalSummaryRenderer();
        var selected = new[]
        {
            RetrievalRenderingFixtures.CreateCandidate(
                rank: 1,
                documentId: "01",
                chunkId: "01_0",
                chunkIndex: 0,
                similarity: 0.93f,
                documentTitle: "Refund Policy",
                section: "Cooling-Off Window")
        };

        var result = RetrievalRenderingFixtures.CreateRunResult(selected, selectedChunks: selected);

        renderer.WriteSummary(logger, result, run: 1, repeat: 1, includeAnswer: false);

        Assert.Contains(
            sink.Messages,
            message => message.Contains("1. Refund Policy", StringComparison.Ordinal)
                && message.Contains("Cooling-Off Window", StringComparison.Ordinal)
                && message.Contains("doc_id=01", StringComparison.Ordinal)
                && message.Contains("chunk_id=01_0", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteSummary_RendersSelectedChunks_TechnicalOnlyFallback_WhenTitleMissing()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var renderer = new RetrievalSummaryRenderer();
        var selected = new[]
        {
            RetrievalRenderingFixtures.CreateCandidate(
                rank: 1,
                documentId: "02",
                chunkId: "02_7",
                chunkIndex: 7,
                similarity: 0.80f,
                documentTitle: null,
                section: null)
        };

        var result = RetrievalRenderingFixtures.CreateRunResult(selected, selectedChunks: selected);

        renderer.WriteSummary(logger, result, run: 1, repeat: 1, includeAnswer: false);

        Assert.Contains(
            sink.Messages,
            message => message.Contains("1. doc_id=02 chunk_id=02_7 chunk_index=7", StringComparison.Ordinal));
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
