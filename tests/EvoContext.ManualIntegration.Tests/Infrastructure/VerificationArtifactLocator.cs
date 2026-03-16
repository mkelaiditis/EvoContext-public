using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal sealed record VerificationArtifactSnapshot(
    string ScenarioId,
    DateTimeOffset CapturedAtUtc,
    IReadOnlySet<string> ExistingTraceArtifacts);

internal sealed record VerificationArtifactDiscovery(
    string RunId,
    string TraceArtifactPath,
    string OperationalTracePath,
    string ScenarioTraceDirectory,
    string VerificationEvidencePath);

internal sealed class VerificationArtifactLocator
{
    public string GetScenarioTraceDirectory(string scenarioId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        return Path.Combine(ManualIntegrationWorkspace.TraceRootPath, scenarioId);
    }

    public string BuildTraceArtifactPath(string scenarioId, string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return Path.Combine(GetScenarioTraceDirectory(scenarioId), $"{runId}.json");
    }

    public string BuildOperationalTracePath(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return Path.Combine(ManualIntegrationWorkspace.OperationalTraceRootPath, $"{runId}.jsonl");
    }

    public string BuildVerificationEvidencePath(string scenarioId, string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return Path.Combine(GetScenarioTraceDirectory(scenarioId), $"{runId}.verification.json");
    }

    public VerificationArtifactSnapshot CreateTraceArtifactSnapshot(string scenarioId)
    {
        var traceArtifacts = EnumerateTraceArtifacts(scenarioId)
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new VerificationArtifactSnapshot(scenarioId, DateTimeOffset.UtcNow, traceArtifacts);
    }

    public bool TryFindNewTraceArtifact(
        VerificationArtifactSnapshot snapshot,
        out VerificationArtifactDiscovery? discovery,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var scenarioTraceDirectory = GetScenarioTraceDirectory(snapshot.ScenarioId);
        var candidates = EnumerateTraceArtifacts(snapshot.ScenarioId)
            .Select(path => Path.GetFullPath(path))
            .Where(path => !snapshot.ExistingTraceArtifacts.Contains(path))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        if (candidates.Length == 0)
        {
            discovery = null;
            error = $"No new trace artifact was created in {scenarioTraceDirectory}.";
            return false;
        }

        var traceArtifactPath = candidates[0];
        var runId = Path.GetFileNameWithoutExtension(traceArtifactPath);
        discovery = new VerificationArtifactDiscovery(
            runId,
            traceArtifactPath,
            BuildOperationalTracePath(runId),
            scenarioTraceDirectory,
            BuildVerificationEvidencePath(snapshot.ScenarioId, runId));
        error = null;
        return true;
    }

    private IEnumerable<string> EnumerateTraceArtifacts(string scenarioId)
    {
        var scenarioTraceDirectory = GetScenarioTraceDirectory(scenarioId);
        if (!Directory.Exists(scenarioTraceDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(scenarioTraceDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".verification.json", StringComparison.OrdinalIgnoreCase));
    }
}