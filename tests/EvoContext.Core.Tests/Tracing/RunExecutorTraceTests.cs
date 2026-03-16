using EvoContext.Core.Config;
using EvoContext.Core.Context;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Services;
using FakeItEasy;

namespace EvoContext.Core.Tests.Tracing;

public sealed class RunExecutorTraceTests
{
    [Fact]
    public async Task ExecuteAsync_EmitsTraceEventsInRequiredSequence_WithRequiredPayloads()
    {
        var retriever = A.Fake<IRetriever>();
        var scorer = A.Fake<ICandidateScorer>();
        var ranker = A.Fake<ICandidateRanker>();
        var selector = A.Fake<IContextSelector>();
        var packer = A.Fake<IContextPacker>();
        var traceEmitter = A.Fake<ITraceEmitter>();
        var capturedEvents = new List<TraceEvent>();

        var retrievedCandidates = new List<RetrievalCandidate>
        {
            new(
                "query-1",
                1,
                0.42f,
                0.42f,
                "01",
                "01_0",
                0,
                "chunk")
        };

        A.CallTo(() => retriever.RetrieveAsync(A<RetrievalRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<RetrievalCandidate>>(retrievedCandidates));
        A.CallTo(() => scorer.Score(A<IReadOnlyList<RetrievalCandidate>>._))
            .ReturnsLazily(call =>
            {
                var candidates = (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!;
                return candidates
                    .Select(candidate => new ScoredCandidate(candidate, new CandidateScore(candidate.SimilarityScore, 0f, 0f, candidate.SimilarityScore)))
                    .ToList();
            });
        A.CallTo(() => ranker.Rank(A<IReadOnlyList<ScoredCandidate>>._))
            .ReturnsLazily(call => (IReadOnlyList<ScoredCandidate>)call.Arguments[0]!);
        A.CallTo(() => selector.Select(A<IReadOnlyList<RetrievalCandidate>>._, A<int>._))
            .ReturnsLazily(call => (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!);
        A.CallTo(() => packer.Pack(A<IReadOnlyList<RetrievalCandidate>>._))
            .Returns(new EvoContext.Core.Context.ContextPack("chunk", 5, 1, 2200));
        A.CallTo(() => traceEmitter.EmitAsync(A<TraceEvent>._, A<CancellationToken>._))
            .Invokes(call => capturedEvents.Add((TraceEvent)call.Arguments[0]!))
            .Returns(Task.CompletedTask);

        var executor = RunExecutor.ForRun1(
            retriever,
            scorer,
            ranker,
            selector,
            packer,
            traceEmitter,
            CreateSnapshot());

        await executor.ExecuteAsync(
            new RunRequest("scenario-alpha", "test task", RunMode.Run1SimilarityOnly),
            TestContext.Current.CancellationToken);

        Assert.Collection(
            capturedEvents,
            first => Assert.Equal(TraceEventType.RunStarted, first.EventType),
            second => Assert.Equal(TraceEventType.RetrievalCompleted, second.EventType),
            third => Assert.Equal(TraceEventType.ContextSelected, third.EventType),
            fourth => Assert.Equal(TraceEventType.RunFinished, fourth.EventType));

        Assert.Collection(
            capturedEvents,
            first => Assert.Equal(1, first.SequenceIndex),
            second => Assert.Equal(2, second.SequenceIndex),
            third => Assert.Equal(3, third.SequenceIndex),
            fourth => Assert.Equal(4, fourth.SequenceIndex));

        var runId = capturedEvents[0].RunId;
        Assert.All(capturedEvents, traceEvent => Assert.Equal(runId, traceEvent.RunId));

        var runStarted = capturedEvents[0];
        Assert.Equal("scenario-alpha", runStarted.Metadata["scenario_id"]);
        Assert.Equal("test task", runStarted.Metadata["task_text"]);
        Assert.Equal(RunMode.Run1SimilarityOnly.ToString(), runStarted.Metadata["run_mode"]);
        Assert.True(runStarted.Metadata.ContainsKey("timestamp_utc"));

        var retrievalCompleted = capturedEvents[1];
        Assert.Equal(1, retrievalCompleted.Metadata["retrieved_count"]);
        Assert.True(retrievalCompleted.Metadata.ContainsKey("timestamp_utc"));
        var candidatesPayload = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(retrievalCompleted.Metadata["candidates"]!);
        Assert.Single(candidatesPayload);
        var candidatePayload = candidatesPayload[0];
        Assert.Equal("01", candidatePayload["document_id"]);
        Assert.Equal("01_0", candidatePayload["chunk_id"]);
        Assert.Equal(0, candidatePayload["chunk_index"]);
        Assert.Equal(0.42f, Assert.IsType<float>(candidatePayload["similarity_score"]));

        var contextSelected = capturedEvents[2];
        Assert.Equal(1, contextSelected.Metadata["selected_count"]);
        Assert.Equal(5, contextSelected.Metadata["context_character_count"]);
        Assert.True(contextSelected.Metadata.ContainsKey("timestamp_utc"));
        var selectedPayload = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(contextSelected.Metadata["selected"]!);
        Assert.Single(selectedPayload);
        var selectedEntry = selectedPayload[0];
        Assert.Equal("01", selectedEntry["document_id"]);
        Assert.Equal("01_0", selectedEntry["chunk_id"]);
        Assert.Equal(0, selectedEntry["chunk_index"]);

        var runFinished = capturedEvents[3];
        Assert.True(runFinished.Metadata.ContainsKey("timestamp_utc"));
    }

    [Fact]
    public async Task ExecuteAsync_EmitsTraceEventsInOrder_WhenNoCandidatesReturned()
    {
        var retriever = A.Fake<IRetriever>();
        var scorer = A.Fake<ICandidateScorer>();
        var ranker = A.Fake<ICandidateRanker>();
        var selector = A.Fake<IContextSelector>();
        var packer = A.Fake<IContextPacker>();
        var traceEmitter = A.Fake<ITraceEmitter>();
        var capturedEvents = new List<TraceEvent>();

        A.CallTo(() => retriever.RetrieveAsync(A<RetrievalRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<RetrievalCandidate>>(Array.Empty<RetrievalCandidate>()));
        A.CallTo(() => scorer.Score(A<IReadOnlyList<RetrievalCandidate>>._))
            .Returns(Array.Empty<ScoredCandidate>());
        A.CallTo(() => ranker.Rank(A<IReadOnlyList<ScoredCandidate>>._))
            .ReturnsLazily(call => (IReadOnlyList<ScoredCandidate>)call.Arguments[0]!);
        A.CallTo(() => selector.Select(A<IReadOnlyList<RetrievalCandidate>>._, A<int>._))
            .ReturnsLazily(call => (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!);
        A.CallTo(() => packer.Pack(A<IReadOnlyList<RetrievalCandidate>>._))
            .Returns(new EvoContext.Core.Context.ContextPack(string.Empty, 0, 0, 2200));
        A.CallTo(() => traceEmitter.EmitAsync(A<TraceEvent>._, A<CancellationToken>._))
            .Invokes(call => capturedEvents.Add((TraceEvent)call.Arguments[0]!))
            .Returns(Task.CompletedTask);

        var executor = RunExecutor.ForRun1(
            retriever,
            scorer,
            ranker,
            selector,
            packer,
            traceEmitter,
            CreateSnapshot());

        await executor.ExecuteAsync(
            new RunRequest("scenario-empty", "no results", RunMode.Run1SimilarityOnly),
            TestContext.Current.CancellationToken);

        Assert.Collection(
            capturedEvents,
            first => Assert.Equal(TraceEventType.RunStarted, first.EventType),
            second => Assert.Equal(TraceEventType.RetrievalCompleted, second.EventType),
            third => Assert.Equal(TraceEventType.ContextSelected, third.EventType),
            fourth => Assert.Equal(TraceEventType.RunFinished, fourth.EventType));
    }

    private static CoreConfigSnapshot CreateSnapshot()
    {
        return new CoreConfigSnapshot(
            "text-embedding-3-small",
            "gpt-4.1",
            0.0,
            1.0,
            350,
            "cosine",
            1200,
            200,
            10,
            3,
            2200,
            "06");
    }
}
