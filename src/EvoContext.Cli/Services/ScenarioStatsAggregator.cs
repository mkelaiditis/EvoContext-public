using EvoContext.Infrastructure.Models;
using EvoContext.Infrastructure.Services;
using Serilog;
using System.Globalization;

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

    public bool TryCompute(string scenarioId, out ScenarioStats? stats, int? kOverride = null)
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
            .Where(path => !path.EndsWith(".verification.json", StringComparison.Ordinal))
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

        stats = BuildScenarioStats(scenarioId, artifacts, _basePath, _logger, kOverride);
        return true;
    }

    private static ScenarioStats BuildScenarioStats(string scenarioId, IReadOnlyList<TraceArtifact> artifacts, string basePath, ILogger logger, int? kOverride)
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
        var retrievalDiagnostics = BuildRetrievalDiagnostics(scenarioId, artifacts, basePath, logger, kOverride);

        return new ScenarioStats(
            scenarioId,
            artifacts.Count,
            averageRun1,
            averageRun2,
            averageDelta,
            bestScore,
            worstScore,
            retrievalDiagnostics);
    }

    private static RetrievalDiagnostics BuildRetrievalDiagnostics(string scenarioId, IReadOnlyList<TraceArtifact> artifacts, string basePath, ILogger logger, int? kOverride)
    {
        var latestArtifact = artifacts[^1];

        var retrieval = latestArtifact.Retrieval;
        if (retrieval is null
            || retrieval.Run1 is null
            || retrieval.Run2 is null
            || retrieval.Run1.CandidateDocuments is null
            || retrieval.Run2.CandidateDocuments is null)
        {
            return RetrievalDiagnostics.Unavailable("trace retrieval data is missing for one or both runs");
        }

        RelevanceProfile profile;
        try
        {
            profile = new RelevanceProfileLoader(basePath).Load(scenarioId);
        }
        catch (FileNotFoundException)
        {
            return RetrievalDiagnostics.Unavailable("no relevance profile defined for this scenario");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to load relevance profile for diagnostics: {ScenarioId}", scenarioId);
            return RetrievalDiagnostics.Unavailable("relevance profile is malformed or missing required fields");
        }

        if (profile.RelevantDocuments.Count == 0)
        {
            return RetrievalDiagnostics.NotComputable("relevance profile contains zero relevant documents");
        }

        var k = kOverride.HasValue ? Math.Max(1, kOverride.Value) : profile.K;
        var relevant = new HashSet<string>(profile.RelevantDocuments, StringComparer.Ordinal);
        var highlyRelevant = new HashSet<string>(profile.HighlyRelevantDocuments ?? Array.Empty<string>(), StringComparer.Ordinal);

        var run1Metrics = ComputeRunMetrics(retrieval.Run1.CandidateDocuments, k, relevant, highlyRelevant);
        var run2Metrics = ComputeRunMetrics(retrieval.Run2.CandidateDocuments, k, relevant, highlyRelevant);

        var run1RelevantTopK = run1Metrics.TopKDocuments.Where(relevant.Contains).ToHashSet(StringComparer.Ordinal);
        var newlyRetrieved = run2Metrics.TopKDocuments
            .Where(relevant.Contains)
            .Where(doc => !run1RelevantTopK.Contains(doc))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return RetrievalDiagnostics.Available(
            k,
            run1Metrics,
            run2Metrics,
            new RetrievalDiagnosticsDelta(
                run2Metrics.RecallAtK - run1Metrics.RecallAtK,
                run2Metrics.Mrr - run1Metrics.Mrr,
                run2Metrics.NdcgAtK - run1Metrics.NdcgAtK,
                newlyRetrieved));
    }

    private static RetrievalRunMetrics ComputeRunMetrics(
        IReadOnlyList<string> candidateDocuments,
        int k,
        IReadOnlySet<string> relevant,
        IReadOnlySet<string> highlyRelevant)
    {
        var deduped = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var doc in candidateDocuments)
        {
            if (string.IsNullOrWhiteSpace(doc))
            {
                continue;
            }

            if (seen.Add(doc))
            {
                deduped.Add(doc);
            }
        }

        var topK = deduped.Take(Math.Max(0, k)).ToList();
        var relevantHits = topK.Count(relevant.Contains);
        var hitAtK = relevantHits > 0;
        var recallAtK = relevant.Count == 0 ? 0d : (double)relevantHits / relevant.Count;

        var firstRelevantRank = deduped.FindIndex(relevant.Contains);
        var mrr = firstRelevantRank < 0 ? 0d : 1d / (firstRelevantRank + 1);

        var dcg = 0d;
        for (var i = 0; i < topK.Count; i++)
        {
            var gain = highlyRelevant.Contains(topK[i]) ? 2d : (relevant.Contains(topK[i]) ? 1d : 0d);
            if (gain <= 0)
            {
                continue;
            }

            dcg += gain / Math.Log2(i + 2);
        }

        var idealGains = relevant
            .Select(doc => highlyRelevant.Contains(doc) ? 2d : 1d)
            .OrderByDescending(gain => gain)
            .Take(Math.Max(0, k))
            .ToList();
        var idcg = 0d;
        for (var i = 0; i < idealGains.Count; i++)
        {
            idcg += idealGains[i] / Math.Log2(i + 2);
        }

        var ndcgAtK = idcg <= 0 ? 0d : dcg / idcg;

        return new RetrievalRunMetrics(hitAtK, recallAtK, mrr, ndcgAtK, topK);
    }

    public sealed record ScenarioStats(
        string ScenarioId,
        int TotalRuns,
        double AverageScoreRun1,
        double AverageScoreRun2,
        double AverageScoreDelta,
        int BestScore,
        int WorstScore,
        RetrievalDiagnostics RetrievalDiagnostics);

    public sealed record RetrievalDiagnostics(
        string Status,
        string? Reason,
        int? K,
        RetrievalRunMetrics? Run1,
        RetrievalRunMetrics? Run2,
        RetrievalDiagnosticsDelta? Delta)
    {
        public static RetrievalDiagnostics Unavailable(string reason) =>
            new("unavailable", reason, null, null, null, null);

        public static RetrievalDiagnostics NotComputable(string reason) =>
            new("not_computable", reason, null, null, null, null);

        public static RetrievalDiagnostics Available(
            int k,
            RetrievalRunMetrics run1,
            RetrievalRunMetrics run2,
            RetrievalDiagnosticsDelta delta) =>
            new("available", null, k, run1, run2, delta);
    }

    public sealed record RetrievalRunMetrics(
        bool HitAtK,
        double RecallAtK,
        double Mrr,
        double NdcgAtK,
        IReadOnlyList<string> TopKDocuments);

    public sealed record RetrievalDiagnosticsDelta(
        double RecallDelta,
        double MrrDelta,
        double NdcgDelta,
        IReadOnlyList<string> NewlyRetrievedRelevantDocs);
}
