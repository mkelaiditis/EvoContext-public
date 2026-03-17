using EvoContext.Cli.Services;
using EvoContext.Infrastructure.Models;
using Serilog;
using System.Text.Json;

namespace EvoContext.Core.Tests.Cli;

public sealed class ScenarioStatsDiagnosticsFormulaTests
{
    [Fact]
    public void TryCompute_ComputesExpectedMetricValues_ForDeterministicFixture()
    {
        var scenarioId = "stats_phase13_formula_" + Guid.NewGuid().ToString("N")[..8];
        var scenarioRoot = Path.Combine(TestDatasetPaths.RepoRoot, "data", "scenarios", scenarioId);
        var scenarioDocuments = Path.Combine(scenarioRoot, "documents");
        var traceDirectory = Path.Combine(TestDatasetPaths.RepoRoot, "artifacts", "traces", scenarioId);

        Directory.CreateDirectory(scenarioDocuments);
        Directory.CreateDirectory(traceDirectory);

        try
        {
            File.WriteAllText(Path.Combine(scenarioRoot, "scenario.json"), $$"""
{
  "scenario_id": "{{scenarioId}}",
  "display_name": "Stats Phase 13 Formula",
  "dataset_path": "data/scenarios/{{scenarioId}}/documents",
  "primary_query": "What is the refund policy?",
  "fallback_queries": [],
  "run_mode_default": "run2",
  "demo_label": "Stats"
}
""");

            File.WriteAllText(Path.Combine(scenarioRoot, "relevance_profile.json"), """
{
  "k": 3,
  "relevant_documents": ["02", "03", "04", "05", "06"],
  "highly_relevant_documents": ["06"],
  "label_to_document_map": {
    "F1": "02",
    "F2": "06",
    "F3": "05",
    "F4": "04",
    "F5": "03"
  }
}
""");

            WriteArtifact(traceDirectory, new TraceArtifact(
                RunId: scenarioId + "_run1",
                ScenarioId: scenarioId,
                DatasetId: scenarioId,
                Query: "query",
                RunMode: "run2",
                TimestampUtc: "2026-03-10T12:00:00Z",
                RetrievalQueries: new[] { "q1", "q2" },
                CandidatePoolSize: 10,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("02", "02_0", 0, "chunk") },
                ContextSizeChars: 100,
                Answer: "answer",
                ScoreTotal: 70,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 40,
                ScoreRun2: 70,
                ScoreDelta: 30,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>(),
                Retrieval: new TraceArtifactRetrieval(
                    new TraceArtifactRetrievalRun(new[] { "02", "01", "03", "02" }, Ranked: true),
                    new TraceArtifactRetrievalRun(new[] { "02", "06", "05", "06" }, Ranked: true))));

            var aggregator = new ScenarioStatsAggregator(new LoggerConfiguration().CreateLogger(), TestDatasetPaths.RepoRoot);
            var ok = aggregator.TryCompute(scenarioId, out var stats);

            Assert.True(ok);
            Assert.NotNull(stats);
            Assert.Equal("available", stats!.RetrievalDiagnostics.Status);

            var run1 = stats.RetrievalDiagnostics.Run1!;
            var run2 = stats.RetrievalDiagnostics.Run2!;
            var delta = stats.RetrievalDiagnostics.Delta!;

            Assert.Equal(0.4, run1.RecallAtK, 3);
            Assert.Equal(0.6, run2.RecallAtK, 3);
            Assert.Equal(1.0, run1.Mrr, 3);
            Assert.Equal(1.0, run2.Mrr, 3);
            Assert.Equal(0.479, run1.NdcgAtK, 3);
            Assert.Equal(0.882, run2.NdcgAtK, 3);
            Assert.Equal(0.2, delta.RecallDelta, 3);
            Assert.Equal(0.0, delta.MrrDelta, 3);
            Assert.Equal(0.403, delta.NdcgDelta, 3);
            Assert.Equal(new[] { "06", "05" }, delta.NewlyRetrievedRelevantDocs);
        }
        finally
        {
            if (Directory.Exists(traceDirectory))
            {
                Directory.Delete(traceDirectory, recursive: true);
            }

            if (Directory.Exists(scenarioRoot))
            {
                Directory.Delete(scenarioRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryCompute_UsesLatestArtifactRetrievalSnapshot_ForDiagnostics()
    {
        var scenarioId = "stats_phase13_latest_" + Guid.NewGuid().ToString("N")[..8];
        var scenarioRoot = Path.Combine(TestDatasetPaths.RepoRoot, "data", "scenarios", scenarioId);
        var scenarioDocuments = Path.Combine(scenarioRoot, "documents");
        var traceDirectory = Path.Combine(TestDatasetPaths.RepoRoot, "artifacts", "traces", scenarioId);

        Directory.CreateDirectory(scenarioDocuments);
        Directory.CreateDirectory(traceDirectory);

        try
        {
            File.WriteAllText(Path.Combine(scenarioRoot, "scenario.json"), $$"""
{
  "scenario_id": "{{scenarioId}}",
  "display_name": "Stats Phase 13 Latest Artifact",
  "dataset_path": "data/scenarios/{{scenarioId}}/documents",
  "primary_query": "What is the refund policy?",
  "fallback_queries": [],
  "run_mode_default": "run2",
  "demo_label": "Stats"
}
""");

            File.WriteAllText(Path.Combine(scenarioRoot, "relevance_profile.json"), """
{
  "k": 3,
  "relevant_documents": ["02", "03", "04", "05", "06"],
  "highly_relevant_documents": ["06"],
  "label_to_document_map": {
    "F1": "02",
    "F2": "06",
    "F3": "05",
    "F4": "04",
    "F5": "03"
  }
}
""");

            WriteArtifact(traceDirectory, new TraceArtifact(
                RunId: scenarioId + "_20260310T120000Z_a001",
                ScenarioId: scenarioId,
                DatasetId: scenarioId,
                Query: "query",
                RunMode: "run2",
                TimestampUtc: "2026-03-10T12:00:00Z",
                RetrievalQueries: new[] { "q1", "q2" },
                CandidatePoolSize: 10,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("02", "02_0", 0, "chunk") },
                ContextSizeChars: 100,
                Answer: "answer",
                ScoreTotal: 70,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 40,
                ScoreRun2: 70,
                ScoreDelta: 30,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>(),
                Retrieval: new TraceArtifactRetrieval(
                    new TraceArtifactRetrievalRun(new[] { "02", "01", "03" }, Ranked: true),
                    new TraceArtifactRetrievalRun(new[] { "02", "06", "05" }, Ranked: true))));

            WriteArtifact(traceDirectory, new TraceArtifact(
                RunId: scenarioId + "_20260310T120500Z_a002",
                ScenarioId: scenarioId,
                DatasetId: scenarioId,
                Query: "query",
                RunMode: "run2",
                TimestampUtc: "2026-03-10T12:05:00Z",
                RetrievalQueries: new[] { "q1", "q2" },
                CandidatePoolSize: 10,
                SelectedChunks: new[] { new TraceArtifactSelectedChunk("02", "02_0", 0, "chunk") },
                ContextSizeChars: 100,
                Answer: "answer",
                ScoreTotal: 90,
                QuerySuggestions: Array.Empty<string>(),
                ScoreRun1: 60,
                ScoreRun2: 90,
                ScoreDelta: 30,
                MemoryUpdates: Array.Empty<string>(),
                ScenarioResult: new Dictionary<string, object?>(),
                Retrieval: new TraceArtifactRetrieval(
                    new TraceArtifactRetrievalRun(new[] { "04", "03", "01" }, Ranked: true),
                    new TraceArtifactRetrievalRun(new[] { "04", "06", "05" }, Ranked: true))));

            var aggregator = new ScenarioStatsAggregator(new LoggerConfiguration().CreateLogger(), TestDatasetPaths.RepoRoot);
            var ok = aggregator.TryCompute(scenarioId, out var stats);

            Assert.True(ok);
            Assert.NotNull(stats);
            Assert.Equal("available", stats!.RetrievalDiagnostics.Status);
            Assert.Equal(new[] { "04", "03", "01" }, stats.RetrievalDiagnostics.Run1!.TopKDocuments);
            Assert.Equal(new[] { "04", "06", "05" }, stats.RetrievalDiagnostics.Run2!.TopKDocuments);
        }
        finally
        {
            if (Directory.Exists(traceDirectory))
            {
                Directory.Delete(traceDirectory, recursive: true);
            }

            if (Directory.Exists(scenarioRoot))
            {
                Directory.Delete(scenarioRoot, recursive: true);
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
}
