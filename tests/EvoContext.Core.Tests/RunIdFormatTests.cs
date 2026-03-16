using System.Globalization;
using EvoContext.Core.Config;
using EvoContext.Core.Context;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Services;
using FakeItEasy;

namespace EvoContext.Core.Tests;

public sealed class RunIdFormatTests
{
    [Fact]
    public async Task RunId_UsesRequiredFormat()
    {
        var scenarioId = "scenario-alpha";
        var retriever = A.Fake<IRetriever>();
        var scorer = A.Fake<ICandidateScorer>();
        var ranker = A.Fake<ICandidateRanker>();
        var selector = A.Fake<IContextSelector>();
        var packer = A.Fake<IContextPacker>();
        var traceEmitter = A.Fake<ITraceEmitter>();

        A.CallTo(() => retriever.RetrieveAsync(A<RetrievalRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<RetrievalCandidate>>(Array.Empty<RetrievalCandidate>()));
        A.CallTo(() => scorer.Score(A<IReadOnlyList<RetrievalCandidate>>._))
            .ReturnsLazily(call =>
            {
                var candidates = (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!;
                return candidates
                    .Select(candidate => new ScoredCandidate(candidate, new CandidateScore(0f, 0f, 0f, 0f)))
                    .ToList();
            });
        A.CallTo(() => ranker.Rank(A<IReadOnlyList<ScoredCandidate>>._))
            .ReturnsLazily(call => (IReadOnlyList<ScoredCandidate>)call.Arguments[0]!);
        A.CallTo(() => selector.Select(A<IReadOnlyList<RetrievalCandidate>>._, A<int>._))
            .ReturnsLazily(call => (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!);
        A.CallTo(() => packer.Pack(A<IReadOnlyList<RetrievalCandidate>>._))
            .ReturnsLazily(call =>
            {
                return new EvoContext.Core.Context.ContextPack(string.Empty, 0, 0, 2200);
            });

        var executor = RunExecutor.ForRun1(
            retriever,
            scorer,
            ranker,
            selector,
            packer,
            traceEmitter,
            CreateSnapshot());

        var result = await executor.ExecuteAsync(
            new RunRequest(
                scenarioId,
                "test task",
                RunMode.Run1SimilarityOnly),
            TestContext.Current.CancellationToken);

        var segments = result.RunId.Split('_');
        Assert.Equal(3, segments.Length);
        Assert.Equal(scenarioId, segments[0]);
        Assert.True(DateTimeOffset.TryParseExact(
            segments[1],
            "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out _));
        Assert.Matches("^[0-9a-f]{4}$", segments[2]);
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
