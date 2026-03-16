using System.Diagnostics;
using System.Text.Json;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Core.Tests.Cli;

public sealed class ScenarioStatsTests
{
    [Fact]
    public void Stats_WithTwoArtifactsForScenario_ComputesTotalsAndAverages()
    {
        var scenarioId = "stats_phase6_totals_" + Guid.NewGuid().ToString("N")[..8];
        var scenarioDirectory = Path.Combine(TestDatasetPaths.RepoRoot, "artifacts", "traces", scenarioId);
        Directory.CreateDirectory(scenarioDirectory);

        try
        {
            WriteArtifact(scenarioDirectory, new TraceArtifact(
                RunId: scenarioId + "_run1",
                ScenarioId: scenarioId,
                DatasetId: scenarioId,
                Query: "query",
                RunMode: "run2",
                TimestampUtc: "2026-03-10T12:00:00Z",
                RetrievalQueries: new[] { "q1", "q2" },
                CandidatePoolSize: 10,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("01", "01_0", 0, "chunk") },
                ContextSizeChars: 100,
                Answer: "answer",
                ScoreTotal: 70,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 40,
                ScoreRun2: 70,
                ScoreDelta: 30,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>()));

            WriteArtifact(scenarioDirectory, new TraceArtifact(
                RunId: scenarioId + "_run2",
                ScenarioId: scenarioId,
                DatasetId: scenarioId,
                Query: "query",
                RunMode: "run2",
                TimestampUtc: "2026-03-10T12:05:00Z",
                RetrievalQueries: new[] { "q1", "q2" },
                CandidatePoolSize: 12,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("02", "02_0", 0, "chunk") },
                ContextSizeChars: 130,
                Answer: "answer",
                ScoreTotal: 90,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 60,
                ScoreRun2: 90,
                ScoreDelta: 30,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>()));

            var result = RunCli("stats --scenario " + scenarioId);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("total_runs=2", result.Output, StringComparison.Ordinal);
            Assert.Contains("average_score_run1=50", result.Output, StringComparison.Ordinal);
            Assert.Contains("average_score_run2=80", result.Output, StringComparison.Ordinal);
            Assert.Contains("average_score_delta=30", result.Output, StringComparison.Ordinal);
            Assert.Contains("best_score=90", result.Output, StringComparison.Ordinal);
            Assert.Contains("worst_score=70", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(scenarioDirectory))
            {
                Directory.Delete(scenarioDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Stats_WithMultipleScenarios_AggregatesRequestedScenarioOnly()
    {
        var requestedScenarioId = "stats_phase6_requested_" + Guid.NewGuid().ToString("N")[..8];
        var otherScenarioId = "stats_phase6_other_" + Guid.NewGuid().ToString("N")[..8];

        var requestedDirectory = Path.Combine(TestDatasetPaths.RepoRoot, "artifacts", "traces", requestedScenarioId);
        var otherDirectory = Path.Combine(TestDatasetPaths.RepoRoot, "artifacts", "traces", otherScenarioId);
        Directory.CreateDirectory(requestedDirectory);
        Directory.CreateDirectory(otherDirectory);

        try
        {
            WriteArtifact(requestedDirectory, new TraceArtifact(
                RunId: requestedScenarioId + "_run1",
                ScenarioId: requestedScenarioId,
                DatasetId: requestedScenarioId,
                Query: "query",
                RunMode: "run1",
                TimestampUtc: "2026-03-10T12:00:00Z",
                RetrievalQueries: new[] { "q1" },
                CandidatePoolSize: 8,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("01", "01_0", 0, "chunk") },
                ContextSizeChars: 90,
                Answer: "answer",
                ScoreTotal: 55,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 55,
                ScoreRun2: null,
                ScoreDelta: null,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>()));

            WriteArtifact(requestedDirectory, new TraceArtifact(
                RunId: requestedScenarioId + "_wrong",
                ScenarioId: otherScenarioId,
                DatasetId: otherScenarioId,
                Query: "query",
                RunMode: "run1",
                TimestampUtc: "2026-03-10T12:01:00Z",
                RetrievalQueries: new[] { "q1" },
                CandidatePoolSize: 7,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("99", "99_0", 0, "chunk") },
                ContextSizeChars: 80,
                Answer: "answer",
                ScoreTotal: 999,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 999,
                ScoreRun2: null,
                ScoreDelta: null,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>()));

            WriteArtifact(otherDirectory, new TraceArtifact(
                RunId: otherScenarioId + "_run1",
                ScenarioId: otherScenarioId,
                DatasetId: otherScenarioId,
                Query: "query",
                RunMode: "run1",
                TimestampUtc: "2026-03-10T12:02:00Z",
                RetrievalQueries: new[] { "q1" },
                CandidatePoolSize: 9,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("03", "03_0", 0, "chunk") },
                ContextSizeChars: 95,
                Answer: "answer",
                ScoreTotal: 88,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 88,
                ScoreRun2: null,
                ScoreDelta: null,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>()));

            var result = RunCli("stats --scenario " + requestedScenarioId);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("total_runs=1", result.Output, StringComparison.Ordinal);
            Assert.Contains("best_score=55", result.Output, StringComparison.Ordinal);
            Assert.Contains("worst_score=55", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("999", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("best_score=88", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("worst_score=88", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(requestedDirectory))
            {
                Directory.Delete(requestedDirectory, recursive: true);
            }

            if (Directory.Exists(otherDirectory))
            {
                Directory.Delete(otherDirectory, recursive: true);
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
