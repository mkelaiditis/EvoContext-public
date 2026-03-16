using EvoContext.Core.Logging;
using EvoContext.Infrastructure.Services;
using Serilog;

namespace EvoContext.Cli.Utilities;

public static class CliPathResolver
{
    public static bool TryResolveDatasetPath(
        ILogger logger,
        string scenarioId,
        string? datasetOverride,
        out string datasetPath)
    {
        if (!string.IsNullOrWhiteSpace(datasetOverride))
        {
            datasetPath = ResolveScenarioPath(datasetOverride);
            logger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("used_override", true),
                    ("resolved_dataset_path", datasetPath))
                .Debug("Dataset path resolved");
            return true;
        }

        try
        {
            var loader = new ScenarioLoader(Directory.GetCurrentDirectory(), logger);
            var scenario = loader.Load(scenarioId);
            datasetPath = ResolveScenarioPath(scenario.DatasetPath);
            logger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("used_override", false),
                    ("resolved_dataset_path", datasetPath))
                .Debug("Dataset path resolved");
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Scenario resolution failed: {Message}", ex.Message);
            datasetPath = string.Empty;
            return false;
        }
    }

    public static string ResolveScenarioPath(string datasetPath)
    {
        if (Path.IsPathRooted(datasetPath))
        {
            return datasetPath;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), datasetPath));
    }

    public static string BuildScenarioCollectionName(string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("Scenario ID is required.", nameof(scenarioId));
        }

        return $"evocontext_{scenarioId}";
    }

    public static string ExtractScenarioId(string runId)
    {
        var lastSeparator = runId.LastIndexOf('_');
        if (lastSeparator <= 0)
        {
            throw new InvalidOperationException($"Invalid run id format: {runId}");
        }

        var withoutSuffix = runId[..lastSeparator];
        var secondSeparator = withoutSuffix.LastIndexOf('_');
        if (secondSeparator <= 0)
        {
            throw new InvalidOperationException($"Invalid run id format: {runId}");
        }

        return withoutSuffix[..secondSeparator];
    }
}
