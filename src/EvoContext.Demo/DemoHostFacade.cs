using EvoContext.Cli.Services;
using EvoContext.Cli.Utilities;
using EvoContext.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace EvoContext.Demo;

public sealed class DemoHostFacade
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly Func<ILogger> _screenLoggerFactory;

    public DemoHostFacade(ILogger logger, IConfiguration configuration, Func<ILogger>? screenLoggerFactory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _screenLoggerFactory = screenLoggerFactory ?? CreateScreenLogger;
    }

    public static bool ParseRunMode(string mode)
    {
        if (string.Equals(mode, "run2", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(mode, "run1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new ArgumentException("run requires --mode <run1|run2>.", nameof(mode));
    }

    public int Ingest(string[] args)
    {
        var (scenarioId, datasetOverride) = CliArgumentParser.ParseScenarioDatasetArgs(args);
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            _logger.Error("ingest requires --scenario <id>.");
            return 2;
        }

        if (!CliPathResolver.TryResolveDatasetPath(_logger, scenarioId, datasetOverride, out var datasetPath))
        {
            return 2;
        }

        return CreateExecutor().IngestAsync(datasetPath).GetAwaiter().GetResult();
    }

    public int Embed(string[] args)
    {
        var (scenarioId, datasetOverride) = CliArgumentParser.ParseScenarioDatasetArgs(args);
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            _logger.Error("embed requires --scenario <id>.");
            return 2;
        }

        if (!CliPathResolver.TryResolveDatasetPath(_logger, scenarioId, datasetOverride, out var datasetPath))
        {
            return 2;
        }

        return CreateExecutor().EmbedAsync(scenarioId, datasetPath).GetAwaiter().GetResult();
    }

    public int Run1(string[] args)
    {
        var (scenarioId, queryText, repeat) = CliArgumentParser.ParseRunWithRepeatArgs(args);
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            _logger.Error("run1 requires --scenario <id>.");
            return 2;
        }

        var scenario = new ScenarioLoader(Directory.GetCurrentDirectory(), _logger).Load(scenarioId);
        var resolvedQuery = string.IsNullOrWhiteSpace(queryText) ? scenario.PrimaryQuery : queryText;
        return CreateExecutor().Run1Async(scenarioId, resolvedQuery, repeat).GetAwaiter().GetResult();
    }

    public int Run3(string[] args)
    {
        var (scenarioId, queryText, repeat) = CliArgumentParser.ParseRunWithRepeatArgs(args);
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            _logger.Error("run3 requires --scenario <id>.");
            return 2;
        }

        var scenario = new ScenarioLoader(Directory.GetCurrentDirectory(), _logger).Load(scenarioId);
        var resolvedQuery = string.IsNullOrWhiteSpace(queryText) ? scenario.PrimaryQuery : queryText;
        return CreateExecutor().Run3Async(scenarioId, resolvedQuery, repeat).GetAwaiter().GetResult();
    }

    public int Run4(string[] args)
    {
        var inputPath = CliArgumentParser.ParseRun4InputPath(args);
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            _logger.Error("run4 requires --input <path>.");
            return 2;
        }

        return CreateExecutor().Run4Async(inputPath).GetAwaiter().GetResult();
    }

    public int Run5(string[] args)
    {
        var (scenarioId, queryText, repeat) = CliArgumentParser.ParseRun5Args(args);
        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(queryText))
        {
            _logger.Error("run5 requires --scenario <id> and --query <text>.");
            return 2;
        }

        return CreateExecutor().Run5Async(scenarioId, queryText, repeat).GetAwaiter().GetResult();
    }

    public int Run(string[] args)
    {
        var (scenarioId, queryText, mode, repeat) = CliArgumentParser.ParseRunArgs(args);
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            _logger.Error("run requires --scenario <id>.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            _logger.Error("run requires --mode <run1|run2>.");
            return 2;
        }

        try
        {
            var allowRun2 = ParseRunMode(mode);
            var scenario = new ScenarioLoader(Directory.GetCurrentDirectory(), _logger).Load(scenarioId);
            var resolvedQuery = string.IsNullOrWhiteSpace(queryText) ? scenario.PrimaryQuery : queryText;

            return CreateRunner()
                .RunScenarioAsync(scenario.ScenarioId, resolvedQuery, allowRun2, repeat)
                .GetAwaiter()
                .GetResult();
        }
        catch (ArgumentException)
        {
            _logger.Error("run requires --mode <run1|run2>.");
            return 2;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "run failed: {Message}", ex.Message);
            return 2;
        }
    }

    public int Replay(string[] args)
    {
        var runId = CliArgumentParser.ParseReplayRunId(args);
        if (string.IsNullOrWhiteSpace(runId))
        {
            _logger.Error("replay requires --run-id <run_id>.");
            return 2;
        }

        try
        {
            var scenarioId = CliPathResolver.ExtractScenarioId(runId);
            var tracePath = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "traces", scenarioId, $"{runId}.json");
            if (!File.Exists(tracePath))
            {
                _logger.Error("Trace artifact not found: {TracePath}", tracePath);
                return 2;
            }

            var artifact = new TraceArtifactReader().Read(tracePath);
            var renderer = new DemoRunRenderer(_screenLoggerFactory());
            new ReplayRenderer().Render(renderer, artifact);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "replay failed: {Message}", ex.Message);
            return 2;
        }
    }

    public int Stats(string[] args)
    {
        var scenarioId = CliArgumentParser.ParseStatsScenarioId(args);
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            _logger.Error("stats requires --scenario <id>.");
            return 2;
        }

        var aggregator = new ScenarioStatsAggregator(_logger);
        if (!aggregator.TryCompute(scenarioId, out var stats) || stats is null)
        {
            return 2;
        }

        var screenLogger = _screenLoggerFactory();
        screenLogger.Information("Scenario stats: scenario_id={ScenarioId}", stats.ScenarioId);
        screenLogger.Information("total_runs={TotalRuns}", stats.TotalRuns);
        screenLogger.Information("average_score_run1={AverageRun1}", stats.AverageScoreRun1);
        screenLogger.Information("average_score_run2={AverageRun2}", stats.AverageScoreRun2);
        screenLogger.Information("average_score_delta={AverageDelta}", stats.AverageScoreDelta);
        screenLogger.Information("best_score={BestScore}", stats.BestScore);
        screenLogger.Information("worst_score={WorstScore}", stats.WorstScore);
        return 0;
    }

    private CliCommandExecutor CreateExecutor()
    {
        return new CliCommandExecutor(
            _logger,
            _configuration,
            _screenLoggerFactory,
            static screenLogger => new DemoRunRenderer(screenLogger),
            static screenLogger => new InteractiveStageProgressReporter(screenLogger));
    }

    private ScenarioRunner CreateRunner()
    {
        return new ScenarioRunner(
            _logger,
            _configuration,
            _screenLoggerFactory,
            static screenLogger => new DemoRunRenderer(screenLogger),
            static screenLogger => new InteractiveStageProgressReporter(screenLogger));
    }

    private static ILogger CreateScreenLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}")
            .CreateLogger();
    }
}
