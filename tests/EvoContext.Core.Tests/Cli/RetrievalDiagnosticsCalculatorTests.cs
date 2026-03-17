using EvoContext.Cli.Services;
using EvoContext.Infrastructure.Models;
using Serilog;
using System.Text.Json;

namespace EvoContext.Core.Tests.Cli;

public sealed class RetrievalDiagnosticsCalculatorTests
{
    [Fact]
    public void ComputeRunMetrics_WhenKExceedsCandidateLength_UsesAllCandidatesWithoutFailure()
    {
        var diagnostics = ExecuteDiagnostics(
            profileK: 10,
            relevantDocuments: new[] { "A", "B", "C" },
            highlyRelevantDocuments: new[] { "A" },
            run1Candidates: new[] { "A", "X" },
            run2Candidates: new[] { "X", "B" });

        Assert.Equal("available", diagnostics.Status);
        Assert.Equal(10, diagnostics.K);
        Assert.Equal(new[] { "A", "X" }, diagnostics.Run1!.TopKDocuments);
        Assert.Equal(new[] { "X", "B" }, diagnostics.Run2!.TopKDocuments);
        Assert.Equal(2, diagnostics.Run1.TopKDocuments.Count);
        Assert.Equal(2, diagnostics.Run2.TopKDocuments.Count);
    }

    [Fact]
    public void ComputeRunMetrics_WhenCandidatesContainDuplicates_DeduplicatesBeforeTopKAndScoring()
    {
        var diagnostics = ExecuteDiagnostics(
            profileK: 3,
            relevantDocuments: new[] { "A", "B", "C" },
            highlyRelevantDocuments: Array.Empty<string>(),
            run1Candidates: new[] { "A", "A", "B", "B", "X" },
            run2Candidates: new[] { "A", "B", "X" });

        Assert.Equal("available", diagnostics.Status);
        Assert.Equal(new[] { "A", "B", "X" }, diagnostics.Run1!.TopKDocuments);
        Assert.Equal(2d / 3d, diagnostics.Run1.RecallAtK, 6);
        Assert.Equal(1d, diagnostics.Run1.Mrr, 6);
    }

    [Fact]
    public void ComputeRunMetrics_WhenCandidateListIsEmpty_ReturnsZeroMetrics()
    {
        var diagnostics = ExecuteDiagnostics(
            profileK: 3,
            relevantDocuments: new[] { "A", "B" },
            highlyRelevantDocuments: new[] { "A" },
            run1Candidates: Array.Empty<string>(),
            run2Candidates: Array.Empty<string>());

        Assert.Equal("available", diagnostics.Status);
        Assert.Empty(diagnostics.Run1!.TopKDocuments);
        Assert.False(diagnostics.Run1.HitAtK);
        Assert.Equal(0d, diagnostics.Run1.RecallAtK, 6);
        Assert.Equal(0d, diagnostics.Run1.Mrr, 6);
        Assert.Equal(0d, diagnostics.Run1.NdcgAtK, 6);
    }

    [Fact]
    public void ComputeRunMetrics_WhenNoRelevantDocsInTopK_ProducesZeroHitRecallMrrAndNdcg()
    {
        var diagnostics = ExecuteDiagnostics(
            profileK: 2,
            relevantDocuments: new[] { "A", "B" },
            highlyRelevantDocuments: new[] { "A" },
            run1Candidates: new[] { "X", "Y", "Z" },
            run2Candidates: new[] { "Y", "Z", "Q" });

        Assert.Equal("available", diagnostics.Status);
        Assert.False(diagnostics.Run1!.HitAtK);
        Assert.Equal(0d, diagnostics.Run1.RecallAtK, 6);
        Assert.Equal(0d, diagnostics.Run1.Mrr, 6);
        Assert.Equal(0d, diagnostics.Run1.NdcgAtK, 6);
    }

    [Fact]
    public void ComputeRunMetrics_WhenFirstRelevantIsAtRank3_ComputesMrrAsOneThird()
    {
        var diagnostics = ExecuteDiagnostics(
            profileK: 3,
            relevantDocuments: new[] { "A" },
            highlyRelevantDocuments: Array.Empty<string>(),
            run1Candidates: new[] { "X", "Y", "A" },
            run2Candidates: new[] { "A", "X", "Y" });

        Assert.Equal("available", diagnostics.Status);
        Assert.Equal(1d / 3d, diagnostics.Run1!.Mrr, 6);
        Assert.Equal(1d, diagnostics.Run2!.Mrr, 6);
    }

    [Fact]
    public void ComputeRunMetrics_WhenHighlyRelevantIsMissing_UsesBinaryNdcgFallback()
    {
        var diagnostics = ExecuteDiagnostics(
            profileK: 3,
            relevantDocuments: new[] { "A", "B", "C" },
            highlyRelevantDocuments: null,
            run1Candidates: new[] { "A", "X", "B" },
            run2Candidates: new[] { "A", "B", "C" });

        Assert.Equal("available", diagnostics.Status);
        Assert.Equal(1.5d / 2.1309297535714578d, diagnostics.Run1!.NdcgAtK, 6);
        Assert.Equal(1d, diagnostics.Run2!.NdcgAtK, 6);
    }

    [Fact]
    public void ComputeRunMetrics_WithKEqualsOne_DifferentiatesRankOneVsRankTwo()
    {
        var diagnostics = ExecuteDiagnostics(
            profileK: 1,
            relevantDocuments: new[] { "A" },
            highlyRelevantDocuments: Array.Empty<string>(),
            run1Candidates: new[] { "A", "X" },
            run2Candidates: new[] { "X", "A" });

        Assert.Equal("available", diagnostics.Status);

        Assert.True(diagnostics.Run1!.HitAtK);
        Assert.Equal(1d, diagnostics.Run1.RecallAtK, 6);
        Assert.Equal(1d, diagnostics.Run1.Mrr, 6);
        Assert.Equal(1d, diagnostics.Run1.NdcgAtK, 6);

        Assert.False(diagnostics.Run2!.HitAtK);
        Assert.Equal(0d, diagnostics.Run2.RecallAtK, 6);
        Assert.Equal(0.5d, diagnostics.Run2.Mrr, 6);
        Assert.Equal(0d, diagnostics.Run2.NdcgAtK, 6);
    }

    private static ScenarioStatsAggregator.RetrievalDiagnostics ExecuteDiagnostics(
        int profileK,
        IReadOnlyList<string> relevantDocuments,
        IReadOnlyList<string>? highlyRelevantDocuments,
        IReadOnlyList<string> run1Candidates,
        IReadOnlyList<string> run2Candidates,
        int? kOverride = null)
    {
        var scenarioId = "stats_phase13_calc_" + Guid.NewGuid().ToString("N")[..8];
        var scenarioRoot = Path.Combine(TestDatasetPaths.RepoRoot, "data", "scenarios", scenarioId);
        var scenarioDocuments = Path.Combine(scenarioRoot, "documents");
        var traceDirectory = Path.Combine(TestDatasetPaths.RepoRoot, "artifacts", "traces", scenarioId);

        Directory.CreateDirectory(scenarioDocuments);
        Directory.CreateDirectory(traceDirectory);

        try
        {
            WriteScenario(scenarioRoot, scenarioId);
            WriteRelevanceProfile(scenarioRoot, profileK, relevantDocuments, highlyRelevantDocuments);
            WriteArtifact(traceDirectory, scenarioId, run1Candidates, run2Candidates);

            var aggregator = new ScenarioStatsAggregator(new LoggerConfiguration().CreateLogger(), TestDatasetPaths.RepoRoot);
            var ok = aggregator.TryCompute(scenarioId, out var stats, kOverride);

            Assert.True(ok);
            Assert.NotNull(stats);

            return stats!.RetrievalDiagnostics;
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

    private static void WriteScenario(string scenarioRoot, string scenarioId)
    {
        var scenario = new
        {
            scenario_id = scenarioId,
            display_name = "Retrieval Diagnostics Calculator Test",
            dataset_path = "data/scenarios/" + scenarioId + "/documents",
            primary_query = "test query",
            fallback_queries = Array.Empty<string>(),
            run_mode_default = "run2",
            demo_label = "Stats"
        };

        var scenarioPath = Path.Combine(scenarioRoot, "scenario.json");
        var scenarioJson = JsonSerializer.Serialize(scenario, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(scenarioPath, scenarioJson);
    }

    private static void WriteRelevanceProfile(
        string scenarioRoot,
        int k,
        IReadOnlyList<string> relevantDocuments,
        IReadOnlyList<string>? highlyRelevantDocuments)
    {
        var labelMap = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < relevantDocuments.Count; i++)
        {
            labelMap["R" + (i + 1).ToString()] = relevantDocuments[i];
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["k"] = k,
            ["relevant_documents"] = relevantDocuments,
            ["label_to_document_map"] = labelMap
        };

        if (highlyRelevantDocuments is not null)
        {
            payload["highly_relevant_documents"] = highlyRelevantDocuments;
        }

        var profilePath = Path.Combine(scenarioRoot, "relevance_profile.json");
        var profileJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(profilePath, profileJson);
    }

    private static void WriteArtifact(
        string traceDirectory,
        string scenarioId,
        IReadOnlyList<string> run1Candidates,
        IReadOnlyList<string> run2Candidates)
    {
        var artifact = new TraceArtifact(
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
                new TraceArtifactRetrievalRun(run1Candidates, Ranked: true),
                new TraceArtifactRetrievalRun(run2Candidates, Ranked: true)));

        var path = Path.Combine(traceDirectory, artifact.RunId + ".json");
        var json = JsonSerializer.Serialize(artifact, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }
}
