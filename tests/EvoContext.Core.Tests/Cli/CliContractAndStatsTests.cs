using System.Diagnostics;
using System.Text.Json;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Core.Tests.Cli;

public sealed class CliContractAndStatsTests
{
    [Fact]
    public void Run_WithUnknownScenario_ReturnsNonZeroExitCode()
    {
        var result = RunCli("run --scenario unknown_phase6_contract --mode run1");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Scenario definition not found", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Stats_AggregatesSelectedScenarioOnly_AndSkipsMismatchedScenarioArtifacts()
    {
        var scenarioA = "stats_phase6_a_" + Guid.NewGuid().ToString("N")[..8];
        var scenarioB = "stats_phase6_b_" + Guid.NewGuid().ToString("N")[..8];

        var repoRoot = TestDatasetPaths.RepoRoot;
        var scenarioADirectory = Path.Combine(repoRoot, "artifacts", "traces", scenarioA);
        var scenarioBDirectory = Path.Combine(repoRoot, "artifacts", "traces", scenarioB);

        Directory.CreateDirectory(scenarioADirectory);
        Directory.CreateDirectory(scenarioBDirectory);

        try
        {
            WriteArtifact(scenarioADirectory, new TraceArtifact(
                RunId: scenarioA + "_run1",
                ScenarioId: scenarioA,
                DatasetId: scenarioA,
                Query: "query",
                RunMode: "run2",
                TimestampUtc: "2026-03-10T12:00:00Z",
                RetrievalQueries: new[] { "q1", "q2" },
                CandidatePoolSize: 10,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("01", "01_0", 0, "chunk") },
                ContextSizeChars: 100,
                Answer: "answer",
                ScoreTotal: 40,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 40,
                ScoreRun2: 70,
                ScoreDelta: 30,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>()));

            WriteArtifact(scenarioADirectory, new TraceArtifact(
                RunId: scenarioA + "_run2",
                ScenarioId: scenarioA,
                DatasetId: scenarioA,
                Query: "query",
                RunMode: "run1",
                TimestampUtc: "2026-03-10T12:05:00Z",
                RetrievalQueries: new[] { "q1" },
                CandidatePoolSize: 8,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("02", "02_0", 0, "chunk") },
                ContextSizeChars: 120,
                Answer: "answer",
                ScoreTotal: 80,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 80,
                ScoreRun2: null,
                ScoreDelta: null,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>()));

            // Mismatched scenario_id inside selected scenario directory should be skipped.
            WriteArtifact(scenarioADirectory, new TraceArtifact(
                RunId: scenarioA + "_wrong_scenario",
                ScenarioId: scenarioB,
                DatasetId: scenarioB,
                Query: "query",
                RunMode: "run1",
                TimestampUtc: "2026-03-10T12:10:00Z",
                RetrievalQueries: new[] { "q1" },
                CandidatePoolSize: 8,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("99", "99_0", 0, "chunk") },
                ContextSizeChars: 120,
                Answer: "answer",
                ScoreTotal: 999,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 999,
                ScoreRun2: null,
                ScoreDelta: null,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>()));

            // Separate scenario directory should not be considered.
            WriteArtifact(scenarioBDirectory, new TraceArtifact(
                RunId: scenarioB + "_run1",
                ScenarioId: scenarioB,
                DatasetId: scenarioB,
                Query: "query",
                RunMode: "run1",
                TimestampUtc: "2026-03-10T12:15:00Z",
                RetrievalQueries: new[] { "q1" },
                CandidatePoolSize: 8,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("03", "03_0", 0, "chunk") },
                ContextSizeChars: 120,
                Answer: "answer",
                ScoreTotal: 95,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 95,
                ScoreRun2: null,
                ScoreDelta: null,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>()));

            var result = RunCli("stats --scenario " + scenarioA);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("total_runs=2", result.Output, StringComparison.Ordinal);
            Assert.Contains("average_score_run1=60", result.Output, StringComparison.Ordinal);
            Assert.Contains("average_score_run2=70", result.Output, StringComparison.Ordinal);
            Assert.Contains("average_score_delta=30", result.Output, StringComparison.Ordinal);
            Assert.Contains("best_score=80", result.Output, StringComparison.Ordinal);
            Assert.Contains("worst_score=40", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("999", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(scenarioADirectory))
            {
                Directory.Delete(scenarioADirectory, recursive: true);
            }

            if (Directory.Exists(scenarioBDirectory))
            {
                Directory.Delete(scenarioBDirectory, recursive: true);
            }
        }
    }

    private static void WriteArtifact(string scenarioDirectory, TraceArtifact artifact)
    {
        var path = Path.Combine(scenarioDirectory, artifact.RunId + ".json");
        var json = JsonSerializer.Serialize(artifact, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }

    private static CliResult RunCli(string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project src/EvoContext.Cli -- " + arguments,
                WorkingDirectory = TestDatasetPaths.RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new CliResult(process.ExitCode, stdout + stderr);
    }

    private sealed record CliResult(int ExitCode, string Output);
}
