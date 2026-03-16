using System.Text.Json;
using EvoContext.Core.Logging;
using EvoContext.Infrastructure.Models;
using Serilog;

namespace EvoContext.Infrastructure.Services;

public sealed class ScenarioLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _basePath;
    private readonly ILogger _logger;

    public ScenarioLoader(string basePath, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            throw new ArgumentException("Base path is required.", nameof(basePath));
        }

        _basePath = ResolveRepoRoot(basePath);
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<ScenarioLoader>();
    }

    public ScenarioDefinition Load(string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("Scenario id is required.", nameof(scenarioId));
        }

        var scenarioRoot = Path.Combine(_basePath, "data", "scenarios", scenarioId);
        var scenarioPath = Path.Combine(scenarioRoot, "scenario.json");
        if (!File.Exists(scenarioPath))
        {
            throw new FileNotFoundException($"Scenario definition not found: {scenarioPath}");
        }

        var json = File.ReadAllText(scenarioPath);
        var definition = JsonSerializer.Deserialize<ScenarioDefinition>(json, JsonOptions);
        if (definition is null)
        {
            throw new InvalidDataException($"Scenario definition could not be parsed: {scenarioPath}");
        }

        var normalized = NormalizeAndValidate(definition, scenarioId, scenarioPath, _basePath);
        var resolvedDatasetPath = ResolveDatasetPath(_basePath, normalized.DatasetPath);

        _logger
            .WithProperties(
                ("scenario_id", normalized.ScenarioId),
                ("scenario_path", scenarioPath),
                ("resolved_dataset_path", resolvedDatasetPath),
                ("primary_query_length", normalized.PrimaryQuery.Length),
                ("run_mode_default", normalized.RunModeDefault))
            .Debug("Scenario definition loaded");

        return normalized;
    }

    private static ScenarioDefinition NormalizeAndValidate(
        ScenarioDefinition definition,
        string expectedScenarioId,
        string scenarioPath,
        string basePath)
    {
        if (string.IsNullOrWhiteSpace(definition.ScenarioId))
        {
            throw new InvalidDataException($"Scenario definition missing scenario_id: {scenarioPath}");
        }

        if (!string.Equals(definition.ScenarioId, expectedScenarioId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Scenario id mismatch in {scenarioPath}. Expected '{expectedScenarioId}', got '{definition.ScenarioId}'.");
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            throw new InvalidDataException($"Scenario definition missing display_name: {scenarioPath}");
        }

        if (string.IsNullOrWhiteSpace(definition.DatasetPath))
        {
            throw new InvalidDataException($"Scenario definition missing dataset_path: {scenarioPath}");
        }

        var resolvedDatasetPath = ResolveDatasetPath(basePath, definition.DatasetPath);
        if (!Directory.Exists(resolvedDatasetPath))
        {
            throw new DirectoryNotFoundException(
                $"Scenario dataset_path not found: {resolvedDatasetPath}");
        }

        if (string.IsNullOrWhiteSpace(definition.PrimaryQuery))
        {
            throw new InvalidDataException($"Scenario definition missing primary_query: {scenarioPath}");
        }

        if (string.IsNullOrWhiteSpace(definition.RunModeDefault))
        {
            throw new InvalidDataException($"Scenario definition missing run_mode_default: {scenarioPath}");
        }

        if (string.IsNullOrWhiteSpace(definition.DemoLabel))
        {
            throw new InvalidDataException($"Scenario definition missing demo_label: {scenarioPath}");
        }

        return definition with
        {
            FallbackQueries = definition.FallbackQueries ?? Array.Empty<string>()
        };
    }

    private static string ResolveDatasetPath(string basePath, string datasetPath)
    {
        if (Path.IsPathRooted(datasetPath))
        {
            return datasetPath;
        }

        return Path.GetFullPath(Path.Combine(basePath, datasetPath));
    }

    private static string ResolveRepoRoot(string basePath)
    {
        var current = new DirectoryInfo(basePath);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "EvoContext.slnx"))
                || Directory.Exists(Path.Combine(current.FullName, ".git"))
                || Directory.Exists(Path.Combine(current.FullName, ".specify")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found from base path.");
    }
}
