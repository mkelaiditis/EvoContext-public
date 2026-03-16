using EvoContext.Infrastructure.Models;
using EvoContext.Infrastructure.Services;
using Serilog;

namespace EvoContext.Cli.Services;

public sealed class ScenarioStatsAggregator
{
    private readonly ILogger _logger;
    private readonly string _basePath;

    public ScenarioStatsAggregator(ILogger logger, string? basePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _basePath = string.IsNullOrWhiteSpace(basePath)
            ? Directory.GetCurrentDirectory()
            : basePath;
    }

    public bool TryCompute(string scenarioId, out ScenarioStats? stats)
    {
        var traceDirectory = Path.Combine(
            _basePath,
            "artifacts",
            "traces",
            scenarioId);

        if (!Directory.Exists(traceDirectory))
        {
            _logger.Error("Trace directory not found: {TraceDirectory}", traceDirectory);
            stats = null;
            return false;
        }

        var reader = new TraceArtifactReader();
        var artifacts = new List<TraceArtifact>();
        var files = Directory.GetFiles(traceDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files)
        {
            if (reader.TryRead(file, out var artifact, out var error))
            {
                if (artifact is not null)
                {
                    if (!string.Equals(artifact.ScenarioId, scenarioId, StringComparison.Ordinal))
                    {
                        _logger.Warning(
                            "Skipping trace artifact with mismatched scenario_id: {File} (expected {ExpectedScenarioId}, found {ActualScenarioId})",
                            file,
                            scenarioId,
                            artifact.ScenarioId);
                        continue;
                    }

                    artifacts.Add(artifact);
                }
            }
            else
            {
                _logger.Warning("Skipping malformed trace artifact: {File} ({Error})", file, error);
            }
        }

        if (artifacts.Count == 0)
        {
            _logger.Error("No trace artifacts found for scenario {ScenarioId}.", scenarioId);
            stats = null;
            return false;
        }

        stats = BuildScenarioStats(scenarioId, artifacts);
        return true;
    }

    private static ScenarioStats BuildScenarioStats(string scenarioId, IReadOnlyList<TraceArtifact> artifacts)
    {
        var averageRun1 = artifacts.Average(artifact => artifact.ScoreRun1);
        var run2Scores = artifacts.Where(artifact => artifact.ScoreRun2.HasValue)
            .Select(artifact => artifact.ScoreRun2!.Value)
            .ToList();
        var averageRun2 = run2Scores.Count == 0 ? 0 : run2Scores.Average();

        var deltaScores = artifacts.Where(artifact => artifact.ScoreDelta.HasValue)
            .Select(artifact => artifact.ScoreDelta!.Value)
            .ToList();
        var averageDelta = deltaScores.Count == 0 ? 0 : deltaScores.Average();

        var bestScore = artifacts.Max(artifact => artifact.ScoreTotal);
        var worstScore = artifacts.Min(artifact => artifact.ScoreTotal);

        return new ScenarioStats(
            scenarioId,
            artifacts.Count,
            averageRun1,
            averageRun2,
            averageDelta,
            bestScore,
            worstScore);
    }

    public sealed record ScenarioStats(
        string ScenarioId,
        int TotalRuns,
        double AverageScoreRun1,
        double AverageScoreRun2,
        double AverageScoreDelta,
        int BestScore,
        int WorstScore);
}
