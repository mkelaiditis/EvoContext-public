using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Logging;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace EvoContext.Cli.Services;

public sealed class ScenarioRunner
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly Func<ILogger> _screenLoggerFactory;
    private readonly Func<ILogger, IRunRenderer> _rendererFactory;
    private readonly Func<ILogger, IStageProgressReporter>? _stageProgressReporterFactory;
    private readonly Func<
        ILogger,
        IConfiguration,
        string,
        string,
        int,
        bool,
        ITraceEmitter?,
        IStageProgressReporter?,
        Task<Run5ExecutionResult>> _run5Executor;

    public ScenarioRunner(ILogger logger, IConfiguration configuration, Func<ILogger> screenLoggerFactory)
        : this(logger, configuration, screenLoggerFactory, rendererFactory: null, stageProgressReporterFactory: null)
    {
    }

    public ScenarioRunner(
        ILogger logger,
        IConfiguration configuration,
        Func<ILogger> screenLoggerFactory,
        Func<ILogger, IRunRenderer>? rendererFactory = null)
        : this(logger, configuration, screenLoggerFactory, rendererFactory, stageProgressReporterFactory: null)
    {
    }

    public ScenarioRunner(
        ILogger logger,
        IConfiguration configuration,
        Func<ILogger> screenLoggerFactory,
        Func<ILogger, IRunRenderer>? rendererFactory,
        Func<ILogger, IStageProgressReporter>? stageProgressReporterFactory)
        : this(
            logger,
            configuration,
            screenLoggerFactory,
            rendererFactory,
            stageProgressReporterFactory,
            run5Executor: null)
    {
    }

    public ScenarioRunner(
        ILogger logger,
        IConfiguration configuration,
        Func<ILogger> screenLoggerFactory,
        Func<ILogger, IRunRenderer>? rendererFactory,
        Func<ILogger, IStageProgressReporter>? stageProgressReporterFactory,
        Func<
            ILogger,
            IConfiguration,
            string,
            string,
            int,
            bool,
            ITraceEmitter?,
            IStageProgressReporter?,
            Task<Run5ExecutionResult>>? run5Executor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _screenLoggerFactory = screenLoggerFactory ?? throw new ArgumentNullException(nameof(screenLoggerFactory));
        _rendererFactory = rendererFactory ?? (static logger => new OperatorRenderer(logger));
        _stageProgressReporterFactory = stageProgressReporterFactory;
        _run5Executor = run5Executor ?? Run5ServiceFactory.ExecuteRun5Async;
    }

    public async Task<int> RunScenarioAsync(
        string scenarioId,
        string queryText,
        bool allowRun2,
        int repeat = 1)
    {
        _logger
            .WithProperties(
                ("scenario_id", scenarioId),
                ("repeat", repeat),
                ("allow_run2", allowRun2),
                ("query_length", queryText.Length))
            .Debug("Scenario runner starting");

        var screenLogger = _screenLoggerFactory();
        var stageProgressReporter = _stageProgressReporterFactory?.Invoke(screenLogger);
        var summaries = new List<RunSummary>();

        for (var i = 1; i <= repeat; i++)
        {
            if (repeat > 1)
            {
                screenLogger.Information("Run {Run}/{Repeat}", i, repeat);
            }

            var renderer = _rendererFactory(screenLogger);
            var rendererTraceEmitter = new RendererTraceEmitter(renderer);

                var execution = await _run5Executor(
                    _logger,
                    _configuration,
                    scenarioId,
                    queryText,
                        1,
                    allowRun2,
                    rendererTraceEmitter,
                    stageProgressReporter)
                .ConfigureAwait(false);

            await WriteTraceArtifactsAsync(scenarioId, queryText, execution.Runs, _logger).ConfigureAwait(false);

            foreach (var run in execution.Runs)
            {
                var summary = BuildRunSummary(run);
                summaries.Add(summary);
                renderer.OnRunComplete(summary);
            }
        }

        if (repeat > 1 && summaries.Count > 0)
        {
            screenLogger.Information(string.Empty);
            screenLogger.Information("Repeat summary: {Count} runs", summaries.Count);
            for (var i = 0; i < summaries.Count; i++)
            {
                var s = summaries[i];
                screenLogger.Information(
                    "  {Index}/{Total} run_id={RunId} score_run1={ScoreRun1} score_run2={ScoreRun2} score_delta={ScoreDelta}",
                    i + 1, summaries.Count, s.RunId, s.ScoreRun1, s.ScoreRun2, s.ScoreDelta);
            }
        }

        return 0;
    }

    public static async Task WriteTraceArtifactsAsync(
        string scenarioId,
        string queryText,
        IReadOnlyList<Run5ExecutionRun> runs,
        ILogger? logger = null)
    {
        var resolvedLogger = logger ?? StructuredLogging.NullLogger;
        var scenario = new ScenarioLoader(Directory.GetCurrentDirectory(), resolvedLogger).Load(scenarioId);
        var writer = new TraceArtifactWriter(Directory.GetCurrentDirectory());
        var verificationEvidenceWriter = new RunVerificationEvidenceWriter(Directory.GetCurrentDirectory());

        foreach (var run in runs)
        {
            var artifact = TraceArtifactBuilder.BuildTraceArtifact(scenario, queryText, run);
            var verificationEvidence = TraceArtifactBuilder.BuildRunVerificationEvidence(scenario, queryText, run);
            await writer.WriteAsync(artifact).ConfigureAwait(false);
            await verificationEvidenceWriter.WriteAsync(verificationEvidence).ConfigureAwait(false);

            resolvedLogger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("run_id", artifact.RunId))
                .Debug("Trace artifact written");

            resolvedLogger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("run_id", verificationEvidence.RunId))
                .Debug("Run verification evidence written");
        }
    }

    public static RunSummary BuildRunSummary(Run5ExecutionRun run)
    {
        var scoreRun2 = run.Run2Evaluation?.ScoreTotal;
        var scoreDelta = run.Run2Evaluation is null ? (int?)null : run.ScoreDelta.ScoreDelta;
        var runMode = run.Run2Result is null ? RunMode.Run1AnswerGeneration : RunMode.Run2FeedbackExpanded;
        var summaryRunId = run.Run2Result?.RunId ?? run.Run1Result.RunId;

        return new RunSummary(
            summaryRunId,
            run.Run1Result.RunRequestSnapshot.ScenarioId,
            runMode,
            run.Run1Evaluation.ScoreTotal,
            scoreRun2,
            scoreDelta,
            run.MemoryUpdates);
    }
}
