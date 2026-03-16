using EvoContext.Core.Config;
using EvoContext.Core.Context;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Services;
using FakeItEasy;
using Serilog;
using CoreContextPack = EvoContext.Core.Context.ContextPack;

namespace EvoContext.Core.Tests.Tracing;

public sealed class RunExecutorGenerationTraceTests
{
    [Fact]
    public async Task ExecuteAsync_EmitsGenerationCompletedTraceWithRequiredFields()
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
            .Returns(new CoreContextPack("packed context", 14, 1, 2200));
        A.CallTo(() => traceEmitter.EmitAsync(A<TraceEvent>._, A<CancellationToken>._))
            .Invokes(call => capturedEvents.Add((TraceEvent)call.Arguments[0]!))
            .Returns(Task.CompletedTask);

        var promptBuilder = new Phase3PromptBuilder();
        var generator = new StubAnswerGenerator("A. Summary\nanswer");
        var validator = new AnswerFormatValidator();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        var answerService = new AnswerGenerationService(promptBuilder, generator, validator, logger);

        var executor = RunExecutor.ForRun3(
            retriever,
            scorer,
            ranker,
            selector,
            packer,
            traceEmitter,
            CreateSnapshot(),
            answerService);

        await executor.ExecuteAsync(
            new RunRequest("scenario-alpha", "question text", RunMode.Run3AnswerGeneration),
            TestContext.Current.CancellationToken);

        var generationEvent = capturedEvents.Single(traceEvent => traceEvent.EventType == TraceEventType.GenerationCompleted);
        Assert.Equal(4, generationEvent.SequenceIndex);
        Assert.Equal("question text", generationEvent.Metadata["prompt_question"]);
        Assert.Equal("packed context", generationEvent.Metadata["prompt_context"]);
        Assert.Equal(Phase3PromptTemplate.TemplateVersion, generationEvent.Metadata["prompt_template_version"]);
        Assert.Equal("gpt-4.1", generationEvent.Metadata["generation_model"]);
        Assert.Equal("A. Summary\nanswer", generationEvent.Metadata["raw_model_output"]);

        var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(generationEvent.Metadata["generation_parameters"]!);
        Assert.Equal(0.0, parameters["temperature"]);
        Assert.Equal(1.0, parameters["top_p"]);
        Assert.Equal(350, parameters["max_tokens"]);
    }

    [Fact]
    public async Task ExecuteAsync_AnnouncesInteractiveStagesAndOnlySpinsForIoStages()
    {
        var retriever = A.Fake<IRetriever>();
        var scorer = A.Fake<ICandidateScorer>();
        var ranker = A.Fake<ICandidateRanker>();
        var selector = A.Fake<IContextSelector>();
        var packer = A.Fake<IContextPacker>();
        var traceEmitter = A.Fake<ITraceEmitter>();

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
            .Returns(new CoreContextPack("packed context", 14, 1, 2200));
        A.CallTo(() => traceEmitter.EmitAsync(A<TraceEvent>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);

        var reporter = new RecordingStageProgressReporter();
        var promptBuilder = new Phase3PromptBuilder();
        var generator = new StubAnswerGenerator("A. Summary\nanswer");
        var validator = new AnswerFormatValidator();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        var answerService = new AnswerGenerationService(promptBuilder, generator, validator, logger);

        var executor = RunExecutor.ForRun3(
            retriever,
            scorer,
            ranker,
            selector,
            packer,
            traceEmitter,
            CreateSnapshot(),
            answerService,
            stageProgressReporter: reporter);

        await executor.ExecuteAsync(
            new RunRequest("scenario-alpha", "question text", RunMode.Run3AnswerGeneration),
            TestContext.Current.CancellationToken);

        Assert.Collection(
            reporter.StageMessages,
            first => Assert.Equal("Retrieving context for query...", first),
            second => Assert.Equal("Selecting top chunks...", second),
            third => Assert.Equal("Generating answer...", third));

        Assert.Collection(
            reporter.SpinnerStages,
            first => Assert.Equal("Retrieving context for query...", first),
            second => Assert.Equal("Generating answer...", second));
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

    private sealed class StubAnswerGenerator : IAnswerGenerator
    {
        private readonly string _answer;

        public StubAnswerGenerator(string answer)
        {
            _answer = answer;
        }

        public Task<string> GenerateAnswerAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_answer);
        }
    }

    private sealed class RecordingStageProgressReporter : IStageProgressReporter
    {
        public List<string> StageMessages { get; } = new();

        public List<string> SpinnerStages { get; } = new();

        public bool IsInteractive => true;

        public void ExecuteStage(string stageMessage, Action action)
        {
            StageMessages.Add(stageMessage);
            action();
        }

        public T ExecuteStage<T>(string stageMessage, Func<T> action)
        {
            StageMessages.Add(stageMessage);
            return action();
        }

        public async Task ExecuteStageAsync(string stageMessage, bool showSpinner, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            StageMessages.Add(stageMessage);
            if (showSpinner)
            {
                SpinnerStages.Add(stageMessage);
            }

            await action(cancellationToken);
        }

        public async Task<T> ExecuteStageAsync<T>(string stageMessage, bool showSpinner, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        {
            StageMessages.Add(stageMessage);
            if (showSpinner)
            {
                SpinnerStages.Add(stageMessage);
            }

            return await action(cancellationToken);
        }
    }
}
