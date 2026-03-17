using EvoContext.Cli.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Execution;

public sealed class CliRetrievedCandidatesRenderingTests
{
    [Fact]
    public void WriteSummary_RendersRetrievedCandidatesHeader_AndCandidateBlockOrder()
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
                similarity: 0.91f,
                documentTitle: "Refund Policy",
                section: "Cooling-Off Window"),
            RetrievalRenderingFixtures.CreateCandidate(
                rank: 2,
                documentId: "02",
                chunkId: "02_3",
                chunkIndex: 3,
                similarity: 0.77f,
                documentTitle: null,
                section: null)
        });

        renderer.WriteSummary(logger, result, run: 1, repeat: 1, includeAnswer: false);

        AssertContainsInOrder(
            sink.Messages,
            "Retrieved candidates (Qdrant):",
            "Rank 1",
            "Document: Refund Policy",
            "Section:",
            "doc_id=01",
            "Similarity:",
            "Rank 2",
            "doc_id=02",
            "Similarity:",
            "Retrieved: 2");
    }

    private static void AssertContainsInOrder(IReadOnlyList<string> messages, params string[] expectedFragments)
    {
        var currentIndex = -1;
        foreach (var fragment in expectedFragments)
        {
            var nextIndex = IndexOfFragment(messages, fragment, currentIndex + 1);
            Assert.True(nextIndex >= 0, $"Could not find '{fragment}' after index {currentIndex}. Messages:\n{string.Join("\n", messages)}");
            currentIndex = nextIndex;
        }
    }

    private static int IndexOfFragment(IReadOnlyList<string> messages, string expectedFragment, int startIndex)
    {
        for (var i = startIndex; i < messages.Count; i++)
        {
            if (messages[i].Contains(expectedFragment, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
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
