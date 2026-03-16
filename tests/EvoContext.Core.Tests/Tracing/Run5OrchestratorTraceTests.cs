using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Config;
using EvoContext.Core.Context;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Services;
using FakeItEasy;
using Serilog;
using CoreContextPack = EvoContext.Core.Context.ContextPack;

namespace EvoContext.Core.Tests.Tracing;

public sealed class Run5OrchestratorTraceTests
{
    [Fact]
    public async Task ExecuteAsync_EmitsRun2TriggeredEvent_WhenRun2IsTriggered()
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
            new("q", 2, 0.80f, 0.80f, "02", "02_0", 0, "billing exception context"),
            new("q", 3, 0.70f, 0.70f, "03", "03_0", 0, "processing timeline context")
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

        var querySet = new RetrievalQuerySet(
            "base query",
            new[] { "feedback query 1", "feedback query 2" },
            new[] { "base query", "feedback query 1", "feedback query 2" });

        A.CallTo(() => queryBuilder.Build(A<string>._, A<FeedbackOutput>._))
            .Returns(querySet);

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
            recordingTrace);

        var execution = await orchestrator.ExecuteAsync(
            PolicyRefundEvaluator.PolicyScenarioId,
            "What is the refund policy for annual subscriptions?",
            repeat: 1,
            allowRun2: true,
            TestContext.Current.CancellationToken);

        var run = Assert.Single(execution.Runs);
        Assert.NotNull(run.Run2Result);

        var run2TriggeredEvent = GetSingleEvent(run.Events, TraceEventType.Run2Triggered);
        Assert.Equal(run.Run1Result.RunId, run2TriggeredEvent.RunId);
        Assert.Equal("Run 2 triggered — score below threshold", Assert.IsType<string>(run2TriggeredEvent.Metadata["label"]!));
        Assert.Equal(3, Assert.IsType<int>(run2TriggeredEvent.Metadata["expanded_query_count"]!));

        Assert.Contains(
            recordingTrace.Events,
            evt => evt.EventType == TraceEventType.Run2Triggered && evt.RunId == run.Run1Result.RunId);

        var evaluationEvent = Assert.Single(
            run.Events,
            evt => evt.EventType == TraceEventType.EvaluationCompleted
                && string.Equals(
                    Assert.IsType<string>(evt.Metadata["run_mode"]!),
                    RunMode.Run1AnswerGeneration.ToString(),
                    StringComparison.Ordinal));
        var presentFactLabels = Assert.IsAssignableFrom<IReadOnlyList<string>>(evaluationEvent.Metadata["present_fact_labels"]!);
        Assert.Contains(Phase4RuleTables.PresentCoolingOffWindow, presentFactLabels);
    }

    [Fact]
    public async Task ExecuteAsync_AnnouncesEvaluationAndRun2ExpansionStages()
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
            new("q", 2, 0.80f, 0.80f, "02", "02_0", 0, "billing exception context"),
            new("q", 3, 0.70f, 0.70f, "03", "03_0", 0, "processing timeline context")
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

        var querySet = new RetrievalQuerySet(
            "base query",
            new[] { "feedback query 1", "feedback query 2" },
            new[] { "base query", "feedback query 1", "feedback query 2" });

        A.CallTo(() => queryBuilder.Build(A<string>._, A<FeedbackOutput>._))
            .Returns(querySet);
        A.CallTo(() => candidatePoolMerger.Merge(A<IReadOnlyList<RetrievalCandidate>>._))
            .ReturnsLazily(call => (IReadOnlyList<RetrievalCandidate>)call.Arguments[0]!);

        var reporter = new RecordingStageProgressReporter();
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
            stageProgressReporter: reporter);

        await orchestrator.ExecuteAsync(
            PolicyRefundEvaluator.PolicyScenarioId,
            "What is the refund policy for annual subscriptions?",
            repeat: 1,
            allowRun2: true,
            TestContext.Current.CancellationToken);

        Assert.Collection(
            reporter.StageMessages,
            first => Assert.Equal("Retrieving context for query...", first),
            second => Assert.Equal("Selecting top chunks...", second),
            third => Assert.Equal("Generating answer...", third),
            fourth => Assert.Equal("Evaluating response...", fourth),
            fifth => Assert.Equal("Triggering Run 2 — expanding retrieval queries...", fifth),
            sixth => Assert.Equal("Retrieving context for query...", sixth),
            seventh => Assert.Equal("Selecting top chunks...", seventh),
            eighth => Assert.Equal("Generating answer...", eighth),
            ninth => Assert.Equal("Evaluating response...", ninth));

        Assert.Collection(
            reporter.SpinnerStages,
            first => Assert.Equal("Retrieving context for query...", first),
            second => Assert.Equal("Generating answer...", second),
            third => Assert.Equal("Retrieving context for query...", third),
            fourth => Assert.Equal("Generating answer...", fourth));
    }

    private static IReadOnlyList<ScoredCandidate> ToScored(IReadOnlyList<RetrievalCandidate> candidates)
    {
        return candidates
            .Select(candidate => new ScoredCandidate(
                candidate,
                new CandidateScore(candidate.SimilarityScore, 0f, 0f, candidate.SimilarityScore)))
            .ToList();
    }

    private static TraceEvent GetSingleEvent(IEnumerable<TraceEvent> events, TraceEventType eventType)
    {
        return Assert.Single(events, evt => evt.EventType == eventType);
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

    private sealed class RecordingTraceEmitter : ITraceEmitter
    {
        private readonly List<TraceEvent> _events = new();

        public IReadOnlyList<TraceEvent> Events => _events;

        public Task EmitAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events.Add(traceEvent);
            return Task.CompletedTask;
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
                new[]
                {
                    Phase4RuleTables.PresentCoolingOffWindow,
                    Phase4RuleTables.PresentAnnualProrationRule,
                    Phase4RuleTables.PresentProcessingTimeline,
                    Phase4RuleTables.PresentCancellationProcedure
                },
                new[] { "MISSING_COOLING_OFF_WINDOW" },
                Array.Empty<string>(),
                new ScoreBreakdown(
                    CompletenessPoints: 40,
                    FormatPoints: 20,
                    HallucinationPenalty: 0,
                    AccuracyCapApplied: false));

            return new EvaluationResult(
                input.RunId,
                input.ScenarioId,
                ScoreTotal: 60,
                QuerySuggestions: new[] { "add cooling-off window details" },
                scenarioResult);
        }
    }
}
