using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Config;
using EvoContext.Core.Context;
using EvoContext.Core.Documents;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Services;
using FakeItEasy;
using Serilog;
using System.Reflection;
using CoreContextPack = EvoContext.Core.Context.ContextPack;

namespace EvoContext.Core.Tests.Execution;

public sealed class StageProgressReporterNullSafetyTests
{
    [Fact]
    public async Task RunExecutor_ForRun1_Executes_WhenStageProgressReporterIsNull()
    {
        var retriever = A.Fake<IRetriever>();
        var scorer = A.Fake<ICandidateScorer>();
        var ranker = A.Fake<ICandidateRanker>();
        var selector = A.Fake<IContextSelector>();
        var packer = A.Fake<IContextPacker>();
        var traceEmitter = A.Fake<ITraceEmitter>();

        var candidates = new List<RetrievalCandidate>
        {
            new("q", 1, 0.9f, 0.9f, "01", "01_0", 0, "chunk")
        };

        A.CallTo(() => retriever.RetrieveAsync(A<RetrievalRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<RetrievalCandidate>>(candidates));
        A.CallTo(() => scorer.Score(A<IReadOnlyList<RetrievalCandidate>>._))
            .ReturnsLazily(call => ToScored((IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!));
        A.CallTo(() => ranker.Rank(A<IReadOnlyList<ScoredCandidate>>._))
            .ReturnsLazily(call => (IReadOnlyList<ScoredCandidate>)call.Arguments[0]!);
        A.CallTo(() => selector.Select(A<IReadOnlyList<RetrievalCandidate>>._, A<int>._))
            .ReturnsLazily(call => (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!);
        A.CallTo(() => packer.Pack(A<IReadOnlyList<RetrievalCandidate>>._))
            .Returns(new CoreContextPack("chunk", 5, 1, 2200));
        A.CallTo(() => traceEmitter.EmitAsync(A<TraceEvent>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);

        var executor = RunExecutor.ForRun1(
            retriever,
            scorer,
            ranker,
            selector,
            packer,
            traceEmitter,
            CreateSnapshot(),
            stageProgressReporter: null);

        var result = await executor.ExecuteAsync(
            new RunRequest("policy_refund_v1", "query", RunMode.Run1SimilarityOnly),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("policy_refund_v1", result.RunRequestSnapshot.ScenarioId);
    }

    [Fact]
    public async Task Run3Orchestrator_Executes_WhenStageProgressReporterIsNull()
    {
        var retriever = A.Fake<IRetriever>();
        var scorer = A.Fake<ICandidateScorer>();
        var ranker = A.Fake<ICandidateRanker>();
        var selector = A.Fake<IContextSelector>();
        var packer = A.Fake<IContextPacker>();
        var inMemoryTrace = new InMemoryTraceEmitter();
        var traceEmitter = A.Fake<ITraceEmitter>();

        var candidates = new List<RetrievalCandidate>
        {
            new("q", 1, 0.9f, 0.9f, "01", "01_0", 0, "chunk")
        };

        A.CallTo(() => retriever.RetrieveAsync(A<RetrievalRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<RetrievalCandidate>>(candidates));
        A.CallTo(() => scorer.Score(A<IReadOnlyList<RetrievalCandidate>>._))
            .ReturnsLazily(call => ToScored((IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!));
        A.CallTo(() => ranker.Rank(A<IReadOnlyList<ScoredCandidate>>._))
            .ReturnsLazily(call => (IReadOnlyList<ScoredCandidate>)call.Arguments[0]!);
        A.CallTo(() => selector.Select(A<IReadOnlyList<RetrievalCandidate>>._, A<int>._))
            .ReturnsLazily(call => (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!);
        A.CallTo(() => packer.Pack(A<IReadOnlyList<RetrievalCandidate>>._))
            .Returns(new CoreContextPack("chunk", 5, 1, 2200));
        A.CallTo(() => traceEmitter.EmitAsync(A<TraceEvent>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);

        var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        var answerService = new AnswerGenerationService(
            new Phase3PromptBuilder(),
            new StubAnswerGenerator(),
            new AnswerFormatValidator(),
            logger);

        var orchestrator = new Run3Orchestrator(
            CreateSnapshot(),
            retriever,
            scorer,
            ranker,
            selector,
            packer,
            answerService,
            inMemoryTrace,
            traceEmitter,
            stageProgressReporter: null);

        var execution = await orchestrator.ExecuteAsync(
            "policy_refund_v1",
            "query",
            repeat: 1,
            TestContext.Current.CancellationToken);

        Assert.Single(execution.Runs);
        Assert.False(string.IsNullOrWhiteSpace(execution.Runs[0].Result.Answer));
    }

    [Fact]
    public async Task Run5Orchestrator_Executes_WhenStageProgressReporterIsNull()
    {
        var retriever = A.Fake<IRetriever>();
        var candidateScorer = A.Fake<ICandidateScorer>();
        var run2CandidateScorer = A.Fake<IRun2CandidateScorer>();
        var candidateRanker = A.Fake<ICandidateRanker>();
        var selector = A.Fake<IContextSelector>();
        var packer = A.Fake<IContextPacker>();
        var queryBuilder = A.Fake<IRun2QueryBuilder>();
        var candidatePoolMerger = A.Fake<ICandidatePoolMerger>();

        var candidates = new List<RetrievalCandidate>
        {
            new("q", 1, 0.90f, 0.90f, "01", "01_0", 0, "refund policy context"),
            new("q", 2, 0.80f, 0.80f, "02", "02_0", 0, "billing exception context")
        };

        A.CallTo(() => retriever.RetrieveAsync(A<RetrievalRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<RetrievalCandidate>>(candidates));
        A.CallTo(() => candidateScorer.Score(A<IReadOnlyList<RetrievalCandidate>>._))
            .ReturnsLazily(call => ToScored((IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!));
        A.CallTo(() => run2CandidateScorer.Score(A<IReadOnlyList<RetrievalCandidate>>._, A<UsefulnessMemorySnapshot?>._))
            .ReturnsLazily(call => ToScored((IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!));
        A.CallTo(() => candidateRanker.Rank(A<IReadOnlyList<ScoredCandidate>>._))
            .ReturnsLazily(call => (IReadOnlyList<ScoredCandidate>)call.Arguments[0]!);
        A.CallTo(() => selector.Select(A<IReadOnlyList<RetrievalCandidate>>._, A<int>._))
            .ReturnsLazily(call =>
            {
                var rankedCandidates = (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!;
                var selectionK = (int)call.Arguments[1]!;
                return rankedCandidates.Take(selectionK).ToList();
            });
        A.CallTo(() => packer.Pack(A<IReadOnlyList<RetrievalCandidate>>._))
            .ReturnsLazily(call =>
            {
                var selected = (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!;
                var text = string.Join("\n", selected.Select(candidate => candidate.ChunkText));
                return new CoreContextPack(text, text.Length, selected.Count, 2200);
            });

        A.CallTo(() => queryBuilder.Build(A<string>._, A<FeedbackOutput>._))
            .Returns(new RetrievalQuerySet(
                "base query",
                new[] { "feedback query" },
                new[] { "base query", "feedback query" }));
        A.CallTo(() => candidatePoolMerger.Merge(A<IReadOnlyList<RetrievalCandidate>>._))
            .ReturnsLazily(call => (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!);

        var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        var answerService = new AnswerGenerationService(
            new Phase3PromptBuilder(),
            new StubAnswerGenerator(),
            new AnswerFormatValidator(),
            logger);

        var dispatcher = new ScenarioEvaluatorDispatcher(new IScenarioEvaluator[]
        {
            new StubPolicyEvaluator()
        });

        using var temp = new TempDirectory();
        var usefulnessStore = new UsefulnessMemoryStore(Path.Combine(temp.Path, "usefulness_memory.json"));
        var inMemoryTrace = new InMemoryTraceEmitter();
        var recordingTrace = new RecordingTraceEmitter();

        var orchestrator = new Run5Orchestrator(
            CreateSnapshot(),
            retriever,
            candidateScorer,
            run2CandidateScorer,
            candidateRanker,
            selector,
            packer,
            queryBuilder,
            candidatePoolMerger,
            usefulnessStore,
            answerService,
            dispatcher,
            inMemoryTrace,
            recordingTrace,
            stageProgressReporter: null);

        var execution = await orchestrator.ExecuteAsync(
            PolicyRefundEvaluator.PolicyScenarioId,
            "What is the refund policy for annual subscriptions?",
            repeat: 1,
            allowRun2: false,
            TestContext.Current.CancellationToken);

        Assert.Single(execution.Runs);
    }

    [Fact]
    public async Task EmbeddingPipelineService_InternalStageExecution_IsNullSafe_WhenStageProgressReporterIsNull()
    {
        var config = CreateSnapshot();
        var loader = A.Fake<IDatasetLoader>();
        var probeWriter = A.Fake<IGateAProbeWriter>();
        var traceEmitter = A.Fake<ITraceEmitter>();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

        var chunker = new Chunker(config.ChunkSizeChars, config.ChunkOverlapChars);
        var embedder = new EmbeddingService(config, apiKey: "test-key", logger: logger);
        var indexService = new QdrantIndexService("localhost", 6334, https: false, apiKey: null, collectionName: "test_collection");
        var retriever = new RetrievalService(
            "localhost",
            6334,
            https: false,
            apiKey: null,
            collectionName: "test_collection",
            config,
            embedder,
            logger);

        var service = new EmbeddingPipelineService(
            config,
            "test_collection",
            loader,
            chunker,
            embedder,
            indexService,
            retriever,
            probeWriter,
            traceEmitter,
            logger,
            stageProgressReporter: null);

        var method = typeof(EmbeddingPipelineService)
            .GetMethod("ExecuteStageAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.MakeGenericMethod(typeof(int));

        Assert.NotNull(method);

        var task = (Task<int>)method!.Invoke(
            service,
            new object[]
            {
                "Null-safe stage",
                true,
                (Func<CancellationToken, Task<int>>)(_ => Task.FromResult(42)),
                CancellationToken.None
            })!;

        var result = await task;
        Assert.Equal(42, result);
    }

    private static IReadOnlyList<ScoredCandidate> ToScored(IReadOnlyList<RetrievalCandidate> candidates)
    {
        return candidates
            .Select(candidate => new ScoredCandidate(
                candidate,
                new CandidateScore(candidate.SimilarityScore, 0f, 0f, candidate.SimilarityScore)))
            .ToList();
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
        public Task<string> GenerateAnswerAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                "A. Summary\n" +
                "Policy summary for refund handling.\n" +
                "B. Eligibility Rules\n" +
                "- Rule one.\n" +
                "C. Exceptions\n" +
                "- Exception one.\n" +
                "D. Timeline and Process\n" +
                "- Timeline details.");
        }
    }

    private sealed class StubPolicyEvaluator : IScenarioEvaluator
    {
        public string ScenarioId => PolicyRefundEvaluator.PolicyScenarioId;

        public EvaluationResult Evaluate(EvaluationInput input)
        {
            var scenarioResult = new PolicyRefundScenarioResult(
                new[] { Phase4RuleTables.PresentCoolingOffWindow },
                Array.Empty<string>(),
                Array.Empty<string>(),
                new ScoreBreakdown(40, 20, 0, false));

            return new EvaluationResult(
                input.RunId,
                input.ScenarioId,
                95,
                Array.Empty<string>(),
                scenarioResult);
        }
    }

    private sealed class RecordingTraceEmitter : ITraceEmitter
    {
        public Task EmitAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
