using EvoContext.Core.Config;
using EvoContext.Core.Context;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Services;
using FakeItEasy;
using Serilog;
using CoreContextPack = EvoContext.Core.Context.ContextPack;

namespace EvoContext.Core.Tests;

public sealed class Run3OrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_AllowsRepeatWhenAnswersMatch()
    {
        var orchestrator = BuildOrchestrator(new[]
        {
            TestAnswerBuilder.BuildAnswer(160),
            TestAnswerBuilder.BuildAnswer(160)
        });

        var execution = await orchestrator.ExecuteAsync(
            "scenario-alpha",
            "question",
            2,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, execution.Runs.Count);
        Assert.All(execution.Runs, run => Assert.Equal(TestAnswerBuilder.BuildAnswer(160), run.Result.Answer));
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsWhenAnswersDifferAcrossRepeats()
    {
        var orchestrator = BuildOrchestrator(new[]
        {
            TestAnswerBuilder.BuildAnswer(160),
            TestAnswerBuilder.BuildAnswer(161)
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.ExecuteAsync(
            "scenario-alpha",
            "question",
            2,
            TestContext.Current.CancellationToken));

        Assert.Contains("Determinism check failed", exception.Message);
    }

    private static Run3Orchestrator BuildOrchestrator(IReadOnlyList<string> answers)
    {
        var retriever = A.Fake<IRetriever>();
        var scorer = A.Fake<ICandidateScorer>();
        var ranker = A.Fake<ICandidateRanker>();
        var selector = A.Fake<IContextSelector>();
        var packer = A.Fake<IContextPacker>();
        var traceEmitter = A.Fake<ITraceEmitter>();
        var inMemoryTrace = new InMemoryTraceEmitter();

        A.CallTo(() => retriever.RetrieveAsync(A<RetrievalRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<RetrievalCandidate>>(Array.Empty<RetrievalCandidate>()));
        A.CallTo(() => scorer.Score(A<IReadOnlyList<RetrievalCandidate>>._))
            .Returns(Array.Empty<ScoredCandidate>());
        A.CallTo(() => ranker.Rank(A<IReadOnlyList<ScoredCandidate>>._))
            .ReturnsLazily(call => (IReadOnlyList<ScoredCandidate>)call.Arguments[0]!);
        A.CallTo(() => selector.Select(A<IReadOnlyList<RetrievalCandidate>>._, A<int>._))
            .ReturnsLazily(call => (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!);
        A.CallTo(() => packer.Pack(A<IReadOnlyList<RetrievalCandidate>>._))
            .Returns(new CoreContextPack(string.Empty, 0, 0, 2200));

        var promptBuilder = new Phase3PromptBuilder();
        var generator = new SequenceAnswerGenerator(answers);
        var validator = new AnswerFormatValidator();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        var answerService = new AnswerGenerationService(promptBuilder, generator, validator, logger);

        return new Run3Orchestrator(
            CreateSnapshot(),
            retriever,
            scorer,
            ranker,
            selector,
            packer,
            answerService,
            inMemoryTrace,
            traceEmitter);
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

    private sealed class SequenceAnswerGenerator : IAnswerGenerator
    {
        private readonly Queue<string> _answers;

        public SequenceAnswerGenerator(IEnumerable<string> answers)
        {
            _answers = new Queue<string>(answers);
        }

        public Task<string> GenerateAnswerAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_answers.Dequeue());
        }
    }
}
