using EvoContext.Cli.Utilities;
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
using Serilog.Core;
using Serilog.Events;
using CoreContextPack = EvoContext.Core.Context.ContextPack;

namespace EvoContext.Core.Tests.Tracing;

public sealed class DebugLoggingBehaviorTests
{
	[Fact]
	public void TryResolveDatasetPath_EmitsStructuredScenarioResolutionDebugLog()
	{
		var logger = CreateLogger(out var sink);
		var originalDirectory = Directory.GetCurrentDirectory();
		Directory.SetCurrentDirectory(TestDatasetPaths.RepoRoot);

		try
		{
			var resolved = CliPathResolver.TryResolveDatasetPath(
				logger,
				PolicyRefundEvaluator.PolicyScenarioId,
				datasetOverride: null,
				out var datasetPath);

			Assert.True(resolved);
			Assert.Equal(
				Path.Combine(TestDatasetPaths.RepoRoot, "data", "scenarios", PolicyRefundEvaluator.PolicyScenarioId, "documents"),
				datasetPath);

			var debugEvent = AssertSingleEvent(sink, "Dataset path resolved");
			Assert.Equal(LogEventLevel.Debug, debugEvent.Level);
			Assert.Equal(PolicyRefundEvaluator.PolicyScenarioId, GetScalarString(debugEvent, "scenario_id"));
			Assert.False(GetScalarBoolean(debugEvent, "used_override"));
			Assert.Equal(datasetPath, GetScalarString(debugEvent, "resolved_dataset_path"));
		}
		finally
		{
			Directory.SetCurrentDirectory(originalDirectory);
		}
	}

	[Fact]
	public async Task ExecuteAsync_EmitsStructuredRunExecutorDebugLogs()
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

		var logger = CreateLogger(out var sink);
		var promptBuilder = new Phase3PromptBuilder();
		var generator = new StubAnswerGenerator("A. Summary\nanswer");
		var validator = new AnswerFormatValidator();
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
			logger);

		await executor.ExecuteAsync(
			new RunRequest("scenario-alpha", "question text", RunMode.Run3AnswerGeneration),
			TestContext.Current.CancellationToken);

		var startEvent = AssertSingleEvent(sink, "Run executor starting");
		Assert.Equal("scenario-alpha", GetScalarString(startEvent, "scenario_id"));
		Assert.Equal(nameof(RunMode.Run3AnswerGeneration), GetScalarString(startEvent, "run_mode"));
		Assert.Equal(13, GetScalarInt(startEvent, "query_length"));

		var retrievalEvent = AssertSingleEvent(sink, "Retrieval ranking completed");
		Assert.Equal(1, GetScalarInt(retrievalEvent, "candidate_count"));
		Assert.Equal("01_0", GetScalarString(retrievalEvent, "top_chunk_id"));

		var contextEvent = AssertSingleEvent(sink, "Context pack created");
		Assert.Equal(1, GetScalarInt(contextEvent, "selected_chunk_count"));
		Assert.Equal(14, GetScalarInt(contextEvent, "context_character_count"));

		var generationEvent = AssertSingleEvent(sink, "Answer generation completed");
		Assert.Equal("scenario-alpha", GetScalarString(generationEvent, "scenario_id"));
		Assert.Equal(17, GetScalarInt(generationEvent, "answer_length"));
		Assert.False(GetScalarBoolean(generationEvent, "word_count_within_range"));
	}

	[Fact]
	public async Task ExecuteAsync_EmitsRun2DecisionAndMemoryLifecycleDebugLogs()
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

		var logger = CreateLogger(out var sink);
		var answerService = new AnswerGenerationService(
			new Phase3PromptBuilder(),
			new StubAnswerGenerator(
				"A. Summary\n" +
				"Policy summary for refund handling.\n" +
				"B. Eligibility Rules\n" +
				"- Rule one.\n" +
				"C. Exceptions\n" +
				"- Exception one.\n" +
				"D. Timeline and Process\n" +
				"- Timeline details."),
			new AnswerFormatValidator(),
			logger);

		var dispatcher = new ScenarioEvaluatorDispatcher(
			new IScenarioEvaluator[] { new SequentialPolicyEvaluator() },
			logger);

		using var temp = new TempDirectory();
		var usefulnessStore = new UsefulnessMemoryStore(Path.Combine(temp.Path, "usefulness_memory.json"), logger);
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
			logger);

		await orchestrator.ExecuteAsync(
			PolicyRefundEvaluator.PolicyScenarioId,
			"What is the refund policy for annual subscriptions?",
			repeat: 1,
			allowRun2: true,
			TestContext.Current.CancellationToken);

		var loadEvent = AssertSingleEvent(sink, "Usefulness memory loaded");
		Assert.False(GetScalarBoolean(loadEvent, "file_found"));
		Assert.Equal(0, GetScalarInt(loadEvent, "item_count"));

		var triggerEvent = AssertSingleEvent(sink, "Run 2 trigger evaluated");
		Assert.Equal(60, GetScalarInt(triggerEvent, "score_run1"));
		Assert.True(GetScalarBoolean(triggerEvent, "should_run2"));
		Assert.Equal("score_below_threshold", GetScalarString(triggerEvent, "reason"));

		var queryEvent = AssertSingleEvent(sink, "Run 2 query set built");
		Assert.Equal(3, GetScalarInt(queryEvent, "expanded_query_count"));

		var deltaEvent = AssertSingleEvent(sink, "Run score delta computed");
		Assert.Equal(60, GetScalarInt(deltaEvent, "score_run1"));
		Assert.Equal(70, GetScalarInt(deltaEvent, "score_run2"));
		Assert.Equal(10, GetScalarInt(deltaEvent, "score_delta"));

		var saveEvent = AssertSingleEvent(sink, "Usefulness memory persisted");
		Assert.True(GetScalarInt(saveEvent, "item_count") > 0);
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

	private static IReadOnlyList<ScoredCandidate> ToScored(IReadOnlyList<RetrievalCandidate> candidates)
	{
		return candidates
			.Select(candidate => new ScoredCandidate(
				candidate,
				new CandidateScore(candidate.SimilarityScore, 0f, 0f, candidate.SimilarityScore)))
			.ToList();
	}

	private static ILogger CreateLogger(out CollectingSink sink)
	{
		sink = new CollectingSink();
		return new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Sink(sink)
			.CreateLogger();
	}

	private static LogEvent AssertSingleEvent(CollectingSink sink, string template)
	{
		return Assert.Single(sink.Events, evt => evt.MessageTemplate.Text == template);
	}

	private static string GetScalarString(LogEvent logEvent, string propertyName)
	{
		var scalar = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
		return Assert.IsType<string>(scalar.Value);
	}

	private static int GetScalarInt(LogEvent logEvent, string propertyName)
	{
		var scalar = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
		return Convert.ToInt32(scalar.Value, System.Globalization.CultureInfo.InvariantCulture);
	}

	private static bool GetScalarBoolean(LogEvent logEvent, string propertyName)
	{
		var scalar = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
		return Assert.IsType<bool>(scalar.Value);
	}

	private sealed class RecordingTraceEmitter : ITraceEmitter
	{
		public Task EmitAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.CompletedTask;
		}
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

	private sealed class SequentialPolicyEvaluator : IScenarioEvaluator
	{
		private int _evaluationCount;

		public string ScenarioId => PolicyRefundEvaluator.PolicyScenarioId;

		public EvaluationResult Evaluate(EvaluationInput input)
		{
			_evaluationCount++;
			var score = _evaluationCount == 1 ? 60 : 70;

			return new EvaluationResult(
				input.RunId,
				input.ScenarioId,
				score,
				new[] { "add cooling-off window details" },
				new PolicyRefundScenarioResult(
					Array.Empty<string>(),
					new[] { "MISSING_COOLING_OFF_WINDOW" },
					Array.Empty<string>(),
					new ScoreBreakdown(
						CompletenessPoints: 40,
						FormatPoints: 20,
						HallucinationPenalty: 0,
						AccuracyCapApplied: false)));
		}
	}

	private sealed class CollectingSink : ILogEventSink
	{
		public List<LogEvent> Events { get; } = new();

		public void Emit(LogEvent logEvent)
		{
			Events.Add(logEvent);
		}
	}
}