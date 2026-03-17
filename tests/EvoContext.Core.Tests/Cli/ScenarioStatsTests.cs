using System.Diagnostics;
using System.Text.Json;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Core.Tests.Cli;

public sealed class ScenarioStatsTests
{
    [Fact]
    public void Stats_WithRetrievalProfileAndTraceSnapshots_EmitsAvailableDiagnostics()
    {
        var scenarioId = "stats_phase13_available_" + Guid.NewGuid().ToString("N")[..8];
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
  "display_name": "Stats Phase 13 Available",
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
                    new TraceArtifactRetrievalRun(new[] { "02", "01", "03" }, Ranked: true),
                    new TraceArtifactRetrievalRun(new[] { "02", "06", "05" }, Ranked: true))));

            var result = RunCli("stats --scenario " + scenarioId);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("retrieval_diagnostics_status=available", result.Output, StringComparison.Ordinal);
            Assert.Contains("retrieval_diagnostics_k=3", result.Output, StringComparison.Ordinal);
            Assert.Contains("run1_recall_at_k=0.4", result.Output, StringComparison.Ordinal);
            Assert.Contains("run2_recall_at_k=0.6", result.Output, StringComparison.Ordinal);
            Assert.Contains("retrieval_recall_delta=0.2", result.Output, StringComparison.Ordinal);
            Assert.Contains("run1_ndcg_at_k=0.479", result.Output, StringComparison.Ordinal);
            Assert.Contains("run2_ndcg_at_k=0.882", result.Output, StringComparison.Ordinal);
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
    public void Stats_WithRun2NewRelevantDocuments_EmitsNewlyRetrievedRelevantDocs()
    {
        var scenarioId = "stats_phase13_newdocs_" + Guid.NewGuid().ToString("N")[..8];
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
  "display_name": "Stats Phase 13 New Docs",
  "dataset_path": "data/scenarios/{{scenarioId}}/documents",
  "primary_query": "How do I fix 502?",
  "fallback_queries": [],
  "run_mode_default": "run2",
  "demo_label": "Stats"
}
""");

            File.WriteAllText(Path.Combine(scenarioRoot, "relevance_profile.json"), """
{
  "k": 3,
  "relevant_documents": ["02", "03", "04"],
  "highly_relevant_documents": ["04"],
  "label_to_document_map": {
    "S1": "04",
    "S2": "02",
    "S3": "03",
    "S4": "03"
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
                    new TraceArtifactRetrievalRun(new[] { "02", "03", "01" }, Ranked: true),
                    new TraceArtifactRetrievalRun(new[] { "02", "04", "03" }, Ranked: true))));

            var result = RunCli("stats --scenario " + scenarioId);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("newly_retrieved_relevant_docs=04", result.Output, StringComparison.Ordinal);
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
    public void Stats_WithKOverride_UsesOverrideInsteadOfProfileDefault()
    {
        var scenarioId = "stats_phase13_koverride_" + Guid.NewGuid().ToString("N")[..8];
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
  "display_name": "Stats Phase 13 K Override",
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
                    new TraceArtifactRetrievalRun(new[] { "02", "01", "03" }, Ranked: true),
                    new TraceArtifactRetrievalRun(new[] { "02", "06", "05" }, Ranked: true))));

            var result = RunCli("stats --scenario " + scenarioId + " --k 2");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("retrieval_diagnostics_k=2", result.Output, StringComparison.Ordinal);
            Assert.Contains("run1_top_k_documents=02,01", result.Output, StringComparison.Ordinal);
            Assert.Contains("run2_top_k_documents=02,06", result.Output, StringComparison.Ordinal);
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
    public void Stats_WithMissingRelevanceProfile_EmitsUnavailableDiagnostics()
    {
        var scenarioId = "stats_phase13_missingprofile_" + Guid.NewGuid().ToString("N")[..8];
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
  "display_name": "Stats Phase 13 Missing Profile",
  "dataset_path": "data/scenarios/{{scenarioId}}/documents",
  "primary_query": "What is the refund policy?",
  "fallback_queries": [],
  "run_mode_default": "run2",
  "demo_label": "Stats"
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
                    new TraceArtifactRetrievalRun(new[] { "02", "01", "03" }, Ranked: true),
                    new TraceArtifactRetrievalRun(new[] { "02", "06", "05" }, Ranked: true))));

            var result = RunCli("stats --scenario " + scenarioId);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("retrieval_diagnostics_status=unavailable", result.Output, StringComparison.Ordinal);
            Assert.Contains("retrieval_diagnostics_reason=no relevance profile defined for this scenario", result.Output, StringComparison.Ordinal);
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
    public void Stats_WithMalformedRelevanceProfile_EmitsUnavailableDiagnostics()
    {
        var scenarioId = "stats_phase13_malformedprofile_" + Guid.NewGuid().ToString("N")[..8];
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
  "display_name": "Stats Phase 13 Malformed Profile",
  "dataset_path": "data/scenarios/{{scenarioId}}/documents",
  "primary_query": "What is the refund policy?",
  "fallback_queries": [],
  "run_mode_default": "run2",
  "demo_label": "Stats"
}
""");

            File.WriteAllText(Path.Combine(scenarioRoot, "relevance_profile.json"), "{ not-valid-json }");

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
                    new TraceArtifactRetrievalRun(new[] { "02", "01", "03" }, Ranked: true),
                    new TraceArtifactRetrievalRun(new[] { "02", "06", "05" }, Ranked: true))));

            var result = RunCli("stats --scenario " + scenarioId);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("retrieval_diagnostics_status=unavailable", result.Output, StringComparison.Ordinal);
            Assert.Contains("retrieval_diagnostics_reason=relevance profile is malformed or missing required fields", result.Output, StringComparison.Ordinal);
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
    public void Stats_WithMissingLegacyTraceRetrievalBlock_EmitsUnavailableDiagnostics()
    {
        var scenarioId = "stats_phase13_missingretrieval_" + Guid.NewGuid().ToString("N")[..8];
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
  "display_name": "Stats Phase 13 Missing Retrieval Block",
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
  "relevant_documents": ["02", "03", "04"],
  "highly_relevant_documents": ["04"],
  "label_to_document_map": {
    "S1": "04",
    "S2": "02",
    "S3": "03"
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
                ScenarioResult: new Dictionary<string, object?>()));

            var result = RunCli("stats --scenario " + scenarioId);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("retrieval_diagnostics_status=unavailable", result.Output, StringComparison.Ordinal);
            Assert.Contains("retrieval_diagnostics_reason=trace retrieval data is missing for one or both runs", result.Output, StringComparison.Ordinal);
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
    public void Stats_WithZeroRelevantDocuments_EmitsNotComputableDiagnostics()
    {
        var scenarioId = "stats_phase13_zerorelevant_" + Guid.NewGuid().ToString("N")[..8];
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
  "display_name": "Stats Phase 13 Zero Relevant",
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
  "relevant_documents": [],
  "highly_relevant_documents": [],
  "label_to_document_map": {}
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
                    new TraceArtifactRetrievalRun(new[] { "02", "01", "03" }, Ranked: true),
                    new TraceArtifactRetrievalRun(new[] { "02", "06", "05" }, Ranked: true))));

            var result = RunCli("stats --scenario " + scenarioId);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("retrieval_diagnostics_status=not_computable", result.Output, StringComparison.Ordinal);
            Assert.Contains("retrieval_diagnostics_reason=relevance profile contains zero relevant documents", result.Output, StringComparison.Ordinal);
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
    public void Stats_WithMalformedTraceRetrievalRunData_EmitsUnavailableDiagnostics()
    {
        var scenarioId = "stats_phase13_malformedretrieval_" + Guid.NewGuid().ToString("N")[..8];
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
  "display_name": "Stats Phase 13 Malformed Retrieval",
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
  "relevant_documents": ["02", "03", "04"],
  "highly_relevant_documents": ["04"],
  "label_to_document_map": {
    "S1": "04",
    "S2": "02",
    "S3": "03"
  }
}
""");

            var rawArtifactPath = Path.Combine(traceDirectory, scenarioId + "_run1.json");
            File.WriteAllText(rawArtifactPath, $$"""
{
  "run_id": "{{scenarioId}}_run1",
  "scenario_id": "{{scenarioId}}",
  "dataset_id": "{{scenarioId}}",
  "query": "query",
  "run_mode": "run2",
  "timestamp_utc": "2026-03-10T12:00:00Z",
  "retrieval_queries": ["q1", "q2"],
  "candidate_pool_size": 10,
  "selected_chunks": [{ "doc_id": "02", "chunk_id": "02_0", "rank": 0, "text": "chunk" }],
  "context_size_chars": 100,
  "answer": "answer",
  "score_total": 70,
  "query_suggestions": [],
  "score_run1": 40,
  "score_run2": 70,
  "score_delta": 30,
  "memory_updates": [],
  "scenario_result": {},
  "retrieval": {
    "run1": { "ranked": true },
    "run2": { "candidate_documents": ["02", "04", "03"], "ranked": true }
  }
}
""");

            var result = RunCli("stats --scenario " + scenarioId);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("retrieval_diagnostics_status=unavailable", result.Output, StringComparison.Ordinal);
            Assert.Contains("retrieval_diagnostics_reason=trace retrieval data is missing for one or both runs", result.Output, StringComparison.Ordinal);
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
