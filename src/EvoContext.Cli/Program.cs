using System;
using System.Linq;
using EvoContext.Cli.Services;
using EvoContext.Cli.Utilities;
using EvoContext.Core.Logging;
using EvoContext.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace EvoContext.Cli;

internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitUsage = 2;
    private const string DefaultEnvironmentName = "Production";

    private static int Main(string[] args)
    {
        var configuration = BuildConfiguration();
        Log.Logger = CreateLogger(configuration);
        try
        {
            var logger = Log.Logger;
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp(logger);
                return ExitOk;
            }

            var command = args[0].ToLowerInvariant();
            logger
                .WithProperties(
                    ("command", command),
                    ("argument_count", args.Length - 1))
                .Debug("CLI command selected");

            return command switch
            {
                "ingest" => Ingest(logger, configuration, args.Skip(1).ToArray()),
                "embed" => Embed(logger, configuration, args.Skip(1).ToArray()),
                "run" => RunScenario(logger, configuration, args.Skip(1).ToArray()),
                "run1" => Run1(logger, configuration, args.Skip(1).ToArray()),
                "run3" => Run3(logger, configuration, args.Skip(1).ToArray()),
                "run4" => Run4(logger, configuration, args.Skip(1).ToArray()),
                "run5" => Run5(logger, configuration, args.Skip(1).ToArray()),
                "replay" => Replay(logger, args.Skip(1).ToArray()),
                "stats" => Stats(logger, args.Skip(1).ToArray()),
                "config" => ShowConfig(configuration),
                _ => UnknownCommand(logger, command)
            };
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static int Ingest(ILogger logger, IConfiguration configuration, string[] args)
    {
        var (scenarioId, datasetOverride) = CliArgumentParser.ParseScenarioDatasetArgs(args);
        logger
            .WithProperties(
                ("command", "ingest"),
                ("scenario_id", scenarioId ?? string.Empty),
                ("has_dataset_override", !string.IsNullOrWhiteSpace(datasetOverride)))
            .Debug("CLI arguments parsed");

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            logger.Error("ingest requires --scenario <id>.");
            return ExitUsage;
        }

        if (!CliPathResolver.TryResolveDatasetPath(logger, scenarioId, datasetOverride, out var datasetPath))
        {
            return ExitUsage;
        }

        return new CliCommandExecutor(logger, configuration, CreateScreenLogger)
            .IngestAsync(datasetPath)
            .GetAwaiter()
            .GetResult();
    }

    private static int Embed(ILogger logger, IConfiguration configuration, string[] args)
    {
        var (scenarioId, datasetOverride) = CliArgumentParser.ParseScenarioDatasetArgs(args);
        logger
            .WithProperties(
                ("command", "embed"),
                ("scenario_id", scenarioId ?? string.Empty),
                ("has_dataset_override", !string.IsNullOrWhiteSpace(datasetOverride)))
            .Debug("CLI arguments parsed");

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            logger.Error("embed requires --scenario <id>.");
            return ExitUsage;
        }

        if (!CliPathResolver.TryResolveDatasetPath(logger, scenarioId, datasetOverride, out var datasetPath))
        {
            return ExitUsage;
        }

        return new CliCommandExecutor(logger, configuration, CreateScreenLogger)
            .EmbedAsync(scenarioId, datasetPath)
            .GetAwaiter()
            .GetResult();
    }

    private static int Run1(ILogger logger, IConfiguration configuration, string[] args)
    {
        var (scenarioId, queryText, repeat) = CliArgumentParser.ParseRunWithRepeatArgs(args);
        logger
            .WithProperties(
                ("command", "run1"),
                ("scenario_id", scenarioId ?? string.Empty),
                ("repeat", repeat),
                ("has_query_override", !string.IsNullOrWhiteSpace(queryText)))
            .Debug("CLI arguments parsed");

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            logger.Error("run1 requires --scenario <id>.");
            return ExitUsage;
        }

        var scenario = new ScenarioLoader(Directory.GetCurrentDirectory(), logger).Load(scenarioId);
        var resolvedQuery = string.IsNullOrWhiteSpace(queryText) ? scenario.PrimaryQuery : queryText;
        return new CliCommandExecutor(logger, configuration, CreateScreenLogger)
            .Run1Async(scenarioId, resolvedQuery, repeat)
            .GetAwaiter()
            .GetResult();
    }

    private static int Run3(ILogger logger, IConfiguration configuration, string[] args)
    {
        var (scenarioId, queryText, repeat) = CliArgumentParser.ParseRunWithRepeatArgs(args);
        logger
            .WithProperties(
                ("command", "run3"),
                ("scenario_id", scenarioId ?? string.Empty),
                ("repeat", repeat),
                ("has_query_override", !string.IsNullOrWhiteSpace(queryText)))
            .Debug("CLI arguments parsed");

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            logger.Error("run3 requires --scenario <id>.");
            return ExitUsage;
        }

        var scenario = new ScenarioLoader(Directory.GetCurrentDirectory(), logger).Load(scenarioId);
        var resolvedQuery = string.IsNullOrWhiteSpace(queryText) ? scenario.PrimaryQuery : queryText;
        return new CliCommandExecutor(logger, configuration, CreateScreenLogger)
            .Run3Async(scenarioId, resolvedQuery, repeat)
            .GetAwaiter()
            .GetResult();
    }

    private static int Run4(ILogger logger, IConfiguration configuration, string[] args)
    {
        var inputPath = CliArgumentParser.ParseRun4InputPath(args);
        logger
            .WithProperties(
                ("command", "run4"),
                ("input_path", inputPath ?? string.Empty))
            .Debug("CLI arguments parsed");

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            logger.Error("run4 requires --input <path>.");
            return ExitUsage;
        }

        return new CliCommandExecutor(logger, configuration, CreateScreenLogger)
            .Run4Async(inputPath)
            .GetAwaiter()
            .GetResult();
    }

    private static int Run5(ILogger logger, IConfiguration configuration, string[] args)
    {
        var (scenarioId, queryText, repeat) = CliArgumentParser.ParseRun5Args(args);
        logger
            .WithProperties(
                ("command", "run5"),
                ("scenario_id", scenarioId ?? string.Empty),
                ("repeat", repeat),
                ("query_length", queryText?.Length ?? 0))
            .Debug("CLI arguments parsed");

        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(queryText))
        {
            logger.Error("run5 requires --scenario <id> and --query <text>.");
            return ExitUsage;
        }

        return new CliCommandExecutor(logger, configuration, CreateScreenLogger)
            .Run5Async(scenarioId, queryText, repeat)
            .GetAwaiter()
            .GetResult();
    }

    private static int RunScenario(ILogger logger, IConfiguration configuration, string[] args)
    {
        var (scenarioId, queryText, mode, repeat) = CliArgumentParser.ParseRunArgs(args);
        logger
            .WithProperties(
                ("command", "run"),
                ("scenario_id", scenarioId ?? string.Empty),
                ("repeat", repeat),
                ("mode", mode ?? string.Empty),
                ("has_query_override", !string.IsNullOrWhiteSpace(queryText)))
            .Debug("CLI arguments parsed");

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            logger.Error("run requires --scenario <id>.");
            return ExitUsage;
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            logger.Error("run requires --mode <run1|run2>.");
            return ExitUsage;
        }

        try
        {
            var scenario = new ScenarioLoader(Directory.GetCurrentDirectory(), logger).Load(scenarioId);
            var resolvedQuery = string.IsNullOrWhiteSpace(queryText) ? scenario.PrimaryQuery : queryText;
            var allowRun2 = string.Equals(mode, "run2", StringComparison.OrdinalIgnoreCase);
            if (!allowRun2 && !string.Equals(mode, "run1", StringComparison.OrdinalIgnoreCase))
            {
                logger.Error("run requires --mode <run1|run2>.");
                return ExitUsage;
            }

                return new ScenarioRunner(
                    logger,
                    configuration,
                    CreateScreenLogger,
                    static screenLogger => new OperatorRenderer(screenLogger))
                .RunScenarioAsync(scenario.ScenarioId, resolvedQuery, allowRun2, repeat)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "run failed: {Message}", ex.Message);
            return ExitUsage;
        }
    }

    private static int Replay(ILogger logger, string[] args)
    {
        var runId = CliArgumentParser.ParseReplayRunId(args);
        logger
            .WithProperties(
                ("command", "replay"),
                ("run_id", runId ?? string.Empty))
            .Debug("CLI arguments parsed");

        if (string.IsNullOrWhiteSpace(runId))
        {
            logger.Error("replay requires --run-id <run_id>.");
            return ExitUsage;
        }

        try
        {
            var scenarioId = CliPathResolver.ExtractScenarioId(runId);
            var tracePath = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "traces", scenarioId, $"{runId}.json");
            if (!File.Exists(tracePath))
            {
                logger.Error("Trace artifact not found: {TracePath}", tracePath);
                return ExitUsage;
            }

            var artifact = new TraceArtifactReader().Read(tracePath);
            var renderer = new OperatorRenderer(CreateScreenLogger());
            new ReplayRenderer().Render(renderer, artifact);
            return ExitOk;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "replay failed: {Message}", ex.Message);
            return ExitUsage;
        }
    }

    private static int Stats(ILogger logger, string[] args)
    {
        var scenarioId = CliArgumentParser.ParseStatsScenarioId(args);
        logger
            .WithProperties(
                ("command", "stats"),
                ("scenario_id", scenarioId ?? string.Empty))
            .Debug("CLI arguments parsed");

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            logger.Error("stats requires --scenario <id>.");
            return ExitUsage;
        }

        var aggregator = new ScenarioStatsAggregator(logger);
        if (!aggregator.TryCompute(scenarioId, out var stats) || stats is null)
        {
            return ExitUsage;
        }

        var screenLogger = CreateScreenLogger();
        screenLogger.Information("Scenario stats: scenario_id={ScenarioId}", stats.ScenarioId);
        screenLogger.Information("total_runs={TotalRuns}", stats.TotalRuns);
        screenLogger.Information("average_score_run1={AverageRun1}", stats.AverageScoreRun1);
        screenLogger.Information("average_score_run2={AverageRun2}", stats.AverageScoreRun2);
        screenLogger.Information("average_score_delta={AverageDelta}", stats.AverageScoreDelta);
        screenLogger.Information("best_score={BestScore}", stats.BestScore);
        screenLogger.Information("worst_score={WorstScore}", stats.WorstScore);
        return ExitOk;
    }

    private static int ShowConfig(IConfiguration configuration)
    {
        var screenLogger = CreateScreenLogger();
        screenLogger.Information("Resolved configuration:");

        var keys = new[]
        {
            "OPENAI_API_KEY",
            "QDRANT_URL",
            "QDRANT_API_KEY",
            "QDRANT_COLLECTION",
            "Phase0:EmbeddingModel",
            "Phase0:GenerationModel",
            "Phase0:Temperature",
            "Phase0:TopP",
            "Phase0:MaxTokens",
            "Phase0:DistanceMetric",
            "Phase0:ChunkSizeChars",
            "Phase0:ChunkOverlapChars",
            "Phase0:RetrievalN",
            "Phase0:SelectionK",
            "Phase0:ContextBudgetChars",
            "Phase0:GateATargetDocId",
        };

        foreach (var key in keys)
        {
            var value = configuration[key];
            var display = string.IsNullOrEmpty(value)
                ? "<empty>"
                : IsSensitiveKey(key) ? "***" : value;
            screenLogger.Information("{Key} = {Value}", key, display);
        }

        return ExitOk;
    }

    private static bool IsSensitiveKey(string key) =>
        key.Contains("KEY", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase);

    private static int UnknownCommand(ILogger logger, string command)
    {
        logger.Error("Unknown command: {Command}", command);
        PrintHelp(logger);
        return ExitUsage;
    }

    private static bool IsHelp(string arg)
    {
        return string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintHelp(ILogger logger)
    {
                foreach (var line in CliHelpText.Text.Split('\n'))
        {
            logger.Information(line.TrimEnd('\r'));
        }
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var environmentName = ResolveEnvironmentName();

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddUserSecrets<CliSecrets>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveEnvironmentName()
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        }

        return string.IsNullOrWhiteSpace(environmentName)
            ? DefaultEnvironmentName
            : environmentName.Trim();
    }

    private static ILogger CreateLogger(IConfiguration configuration)
    {
        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }

    private static ILogger CreateScreenLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}")
            .CreateLogger();
    }
}

internal sealed class CliSecrets
{
}
