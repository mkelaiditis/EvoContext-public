using EvoContext.Cli.Services;
using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Context;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Execution;

public sealed class CliFacadeRendererInjectionTests
{
    [Fact]
    public void CliCommandExecutor_ExposesHostConstructorWithRendererFactorySeam()
    {
        var constructor = typeof(CliCommandExecutor).GetConstructor(new[]
        {
            typeof(ILogger),
            typeof(IConfiguration),
            typeof(Func<ILogger>),
            typeof(Func<ILogger, IRunRenderer>)
        });

        Assert.NotNull(constructor);
    }

    [Fact]
    public void CliCommandExecutor_HasRendererSeamConstructorParameter()
    {
        var hasRendererParameter = typeof(CliCommandExecutor)
            .GetConstructors()
            .SelectMany(static ctor => ctor.GetParameters())
            .Any(static parameter => parameter.ParameterType == typeof(Func<ILogger, IRunRenderer>));

        Assert.True(hasRendererParameter);
    }

    [Fact]
    public void ScenarioRunner_ExposesHostConstructorWithRendererFactorySeam()
    {
        var constructor = typeof(ScenarioRunner).GetConstructor(new[]
        {
            typeof(ILogger),
            typeof(IConfiguration),
            typeof(Func<ILogger>),
            typeof(Func<ILogger, IRunRenderer>)
        });

        Assert.NotNull(constructor);
    }

    [Fact]
    public void ScenarioRunner_HasRendererSeamConstructorParameter()
    {
        var hasRendererParameter = typeof(ScenarioRunner)
            .GetConstructors()
            .SelectMany(static ctor => ctor.GetParameters())
            .Any(static parameter => parameter.ParameterType == typeof(Func<ILogger, IRunRenderer>));

        Assert.True(hasRendererParameter);
    }

    [Fact]
    public async Task ScenarioRunner_UsesFreshRendererPerRepeat_AndDeliversRunComplete()
    {
        using var temp = new TempDirectory();
        var previousCurrentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(temp.Path);

        try
        {
            const string scenarioId = "policy_refund_v1";
            PrepareScenarioFixture(temp.Path, scenarioId);

            var configuration = new ConfigurationBuilder().Build();
            var logger = new LoggerConfiguration().CreateLogger();
            var renderers = new List<CountingRenderer>();

            var runner = new ScenarioRunner(
                logger,
                configuration,
                () => logger,
                rendererFactory: _ =>
                {
                    var renderer = new CountingRenderer();
                    renderers.Add(renderer);
                    return renderer;
                },
                stageProgressReporterFactory: null,
                run5Executor: (_, _, sid, query, _, _, _, _) =>
                    Task.FromResult(CreateExecutionResult(sid, query, runSuffix: Guid.NewGuid().ToString("N"))));

            var exitCode = await runner.RunScenarioAsync(
                scenarioId,
                "What is the refund policy?",
                allowRun2: false,
                repeat: 2);

            Assert.Equal(0, exitCode);
            Assert.Equal(2, renderers.Count);
            Assert.All(renderers, renderer => Assert.Equal(1, renderer.RunCompleteCount));
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCurrentDirectory);
        }
    }

    [Fact]
    public async Task CliCommandExecutor_Run5Async_UsesInjectedRendererFactory_ForSummaryLifecycle()
    {
        using var temp = new TempDirectory();
        var previousCurrentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(temp.Path);

        try
        {
            const string scenarioId = "policy_refund_v1";
            const string query = "What is the refund policy?";
            PrepareScenarioFixture(temp.Path, scenarioId);

            var configuration = new ConfigurationBuilder().Build();
            var logger = new LoggerConfiguration().CreateLogger();
            var renderer = new CountingRenderer();
            var liveEmitterEnabled = false;

            var executor = new CliCommandExecutor(
                logger,
                configuration,
                () => logger,
                rendererFactory: _ => renderer,
                stageProgressReporterFactory: null,
                run5Executor: (_, _, sid, q, repeat, _, liveEmitter, _) =>
                {
                    liveEmitterEnabled = liveEmitter is not null;
                    var runs = Enumerable.Range(1, repeat)
                        .Select(index => CreateExecutionRun(
                            sid,
                            q,
                            runSuffix: string.Concat("run", index.ToString())))
                        .ToList();
                    return Task.FromResult(new Run5ExecutionResult(runs));
                });

            var exitCode = await executor.Run5Async(scenarioId, query, repeat: 3);

            Assert.Equal(0, exitCode);
            Assert.True(liveEmitterEnabled);
            Assert.Equal(3, renderer.RunCompleteCount);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCurrentDirectory);
        }
    }

    [Fact]
    public void RetrievalSummaryRenderer_UsesExplainabilityFriendlyFormatting()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var renderer = new RetrievalSummaryRenderer();

        var candidate = new RetrievalCandidate(
            "q_run2",
            1,
            0.92f,
            0.92f,
            "01",
            "01_0",
            0,
            "chunk text",
            "Refund Policy",
            "Cooling-Off Window");

        var runResult = new RunResult(
            "run-id",
            new RunRequest("policy_refund_v1", "What is the refund policy?", RunMode.Run1SimilarityOnly),
            new RetrievalSummary(
                new[] { candidate },
                new[] { candidate },
                new EvoContext.Core.Context.ContextPack("chunk text", 10, 1, 2200)),
            Answer: null,
            EvaluationResult: null);

        renderer.WriteSummary(logger, runResult, run: 1, repeat: 1, includeAnswer: false);

        Assert.Contains(sink.Messages, message => message.Contains("Retrieved candidates (Qdrant):", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("Document: Refund Policy", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("Section:", StringComparison.Ordinal));
        Assert.Contains(
            sink.Messages,
            message => message.Contains("1. Refund Policy", StringComparison.Ordinal)
                && message.Contains("doc_id=01", StringComparison.Ordinal)
                && message.Contains("chunk_id=01_0", StringComparison.Ordinal));
    }

    [Fact]
    public void RetrievalSummaryRenderer_EmitsRun2Attribution_AndSkipsRun1Attribution()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var renderer = new RetrievalSummaryRenderer();

        var run2Candidate = new RetrievalCandidate(
            "run2_q2",
            1,
            0.81f,
            0.81f,
            "02",
            "02_0",
            0,
            "chunk text",
            null,
            null,
            null);
        var run1Candidate = new RetrievalCandidate(
            "run1_primary",
            2,
            0.79f,
            0.79f,
            "03",
            "03_0",
            0,
            "chunk text",
            null,
            null,
            "base query text");

        var runResult = new RunResult(
            "run-id",
            new RunRequest("policy_refund_v1", "What is the refund policy?", RunMode.Run1SimilarityOnly),
            new RetrievalSummary(
                new[] { run2Candidate, run1Candidate },
                new[] { run2Candidate },
                new EvoContext.Core.Context.ContextPack("chunk text", 10, 1, 2200)),
            Answer: null,
            EvaluationResult: null);

        renderer.WriteSummary(logger, runResult, run: 1, repeat: 1, includeAnswer: false);

        Assert.Contains(sink.Messages, message => message.Contains("Query source: feedback expansion", StringComparison.Ordinal));
        Assert.DoesNotContain(sink.Messages, message => message.Contains("Matched query: base query text", StringComparison.Ordinal));
    }

    private static void PrepareScenarioFixture(string rootPath, string scenarioId)
    {
        Directory.CreateDirectory(Path.Combine(rootPath, ".specify"));

        var datasetPath = Path.Combine("data", "scenarios", scenarioId, "docs");
        Directory.CreateDirectory(Path.Combine(rootPath, datasetPath));

        var scenarioDirectory = Path.Combine(rootPath, "data", "scenarios", scenarioId);
        Directory.CreateDirectory(scenarioDirectory);

        var scenarioJsonPath = Path.Combine(scenarioDirectory, "scenario.json");
        var scenarioJson =
            "{\n" +
            "  \"scenario_id\": \"" + scenarioId + "\",\n" +
            "  \"display_name\": \"Policy Refund\",\n" +
            "  \"dataset_path\": \"" + datasetPath.Replace("\\", "/", StringComparison.Ordinal) + "\",\n" +
            "  \"primary_query\": \"What is the refund policy for annual subscriptions?\",\n" +
            "  \"fallback_queries\": [],\n" +
            "  \"run_mode_default\": \"run2\",\n" +
            "  \"demo_label\": \"Policy Refund Demo\"\n" +
            "}";

        File.WriteAllText(scenarioJsonPath, scenarioJson);
    }

    private static Run5ExecutionResult CreateExecutionResult(string scenarioId, string query, string runSuffix)
    {
        return new Run5ExecutionResult(new[]
        {
            CreateExecutionRun(scenarioId, query, runSuffix)
        });
    }

    private static Run5ExecutionRun CreateExecutionRun(string scenarioId, string query, string runSuffix)
    {
        var runId = string.Concat(scenarioId, "_20990101T000000Z_", runSuffix);
        var runRequest = new RunRequest(scenarioId, query, RunMode.Run1AnswerGeneration);
        var candidate = new RetrievalCandidate("q", 1, 0.9f, 0.9f, "01", "01_0", 0, "chunk text");
        var summary = new RetrievalSummary(
            new[] { candidate },
            new[] { candidate },
            new ContextPack("chunk text", 10, 1, 2200));
        var runResult = new RunResult(runId, runRequest, summary, "A. Summary", null);
        var scoreBreakdown = new ScoreBreakdown(40, 20, 0, false);
        var scenarioResult = new PolicyRefundScenarioResult(
            new[] { Phase4RuleTables.PresentCoolingOffWindow },
            Array.Empty<string>(),
            Array.Empty<string>(),
            scoreBreakdown);
        var evaluation = new EvaluationResult(runId, scenarioId, 95, Array.Empty<string>(), scenarioResult);
        var feedback = new FeedbackOutput(runId, scenarioId, 95, scoreBreakdown, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        var trigger = new Run2Trigger(95, Array.Empty<string>(), ShouldRun: false);

        return new Run5ExecutionRun(
            runResult,
            evaluation,
            feedback,
            trigger,
            Run2QuerySet: null,
            Run2Result: null,
            Run2Evaluation: null,
            Run2Feedback: null,
            new RunScoreDelta(95, 95, 0),
            MemoryUpdates: 0,
            Events: Array.Empty<TraceEvent>());
    }

    private sealed class CountingRenderer : IRunRenderer
    {
        public int RunCompleteCount { get; private set; }

        public void OnEvent(TraceEvent evt)
        {
        }

        public void OnRunComplete(RunSummary summary)
        {
            RunCompleteCount++;
        }
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
