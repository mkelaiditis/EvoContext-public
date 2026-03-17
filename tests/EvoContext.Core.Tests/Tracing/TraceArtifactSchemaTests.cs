using System.Text.Json;
using EvoContext.Core.Evaluation;
using EvoContext.Infrastructure.Models;
using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests.Tracing;

public sealed class TraceArtifactSchemaTests
{
    [Fact]
    public async Task WriteAsync_WritesRequiredTopLevelSchema()
    {
        using var temp = new TempDirectory();
        var writer = new TraceArtifactWriter(temp.Path);

        var artifact = CreatePolicyArtifact();

        await writer.WriteAsync(artifact, TestContext.Current.CancellationToken);

        var path = Path.Combine(temp.Path, "artifacts", "traces", "policy_refund_v1", artifact.RunId + ".json");
        var json = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("run_id", out _));
        Assert.True(root.TryGetProperty("scenario_id", out _));
        Assert.True(root.TryGetProperty("dataset_id", out _));
        Assert.True(root.TryGetProperty("query", out _));
        Assert.True(root.TryGetProperty("run_mode", out _));
        Assert.True(root.TryGetProperty("timestamp_utc", out _));
        Assert.True(root.TryGetProperty("retrieval_queries", out _));
        Assert.True(root.TryGetProperty("candidate_pool_size", out _));
        Assert.True(root.TryGetProperty("selected_chunks", out _));
        Assert.True(root.TryGetProperty("context_size_chars", out _));
        Assert.True(root.TryGetProperty("answer", out _));
        Assert.True(root.TryGetProperty("score_total", out _));
        Assert.True(root.TryGetProperty("query_suggestions", out _));
        Assert.True(root.TryGetProperty("score_run1", out _));
        Assert.True(root.TryGetProperty("score_run2", out _));
        Assert.True(root.TryGetProperty("score_delta", out _));
        Assert.True(root.TryGetProperty("memory_updates", out _));
        Assert.True(root.TryGetProperty("scenario_result", out _));
        Assert.True(root.TryGetProperty("detected_evidence_items", out var detectedEvidenceItems));
        Assert.True(root.TryGetProperty("evidence_block", out var evidenceBlock));
        Assert.True(root.TryGetProperty("retrieval", out var retrieval));
        Assert.True(retrieval.TryGetProperty("run1", out var retrievalRun1));
        Assert.True(retrievalRun1.TryGetProperty("candidate_documents", out var retrievalRun1CandidateDocuments));
        Assert.True(retrievalRun1.TryGetProperty("ranked", out _));
        Assert.Equal(3, retrievalRun1CandidateDocuments.GetArrayLength());
        Assert.Equal("02", retrievalRun1CandidateDocuments[0].GetString());
        Assert.Equal("policy_refund_v1", root.GetProperty("dataset_id").GetString());
        Assert.Equal(1, detectedEvidenceItems.GetArrayLength());
        var firstEvidenceItem = detectedEvidenceItems[0];
        Assert.Equal(Phase4RuleTables.PresentAnnualProrationRule, firstEvidenceItem.GetProperty("fact_label").GetString());
        Assert.Equal("06", firstEvidenceItem.GetProperty("document_id").GetString());
        Assert.Equal("prorated reimbursement", firstEvidenceItem.GetProperty("matched_anchor").GetString());
        Assert.Equal("Customers may receive prorated reimbursement for unused service value.", firstEvidenceItem.GetProperty("extracted_snippet").GetString());
        Assert.Contains("Detected evidence from retrieved context:", evidenceBlock.GetString(), StringComparison.Ordinal);

        var scenarioResult = root.GetProperty("scenario_result");
        Assert.True(scenarioResult.TryGetProperty("present_fact_labels", out _));
        Assert.True(scenarioResult.TryGetProperty("missing_fact_labels", out _));
        Assert.True(scenarioResult.TryGetProperty("hallucination_flags", out _));
        Assert.True(scenarioResult.TryGetProperty("score_breakdown", out var scoreBreakdown));
        Assert.True(scoreBreakdown.TryGetProperty("completeness_points", out _));
        Assert.True(scoreBreakdown.TryGetProperty("format_points", out _));
        Assert.True(scoreBreakdown.TryGetProperty("hallucination_penalty", out _));
        Assert.True(scoreBreakdown.TryGetProperty("accuracy_cap_applied", out _));
    }

    [Fact]
    public async Task WriteAsync_WritesRunbookScenarioResultShape()
    {
        using var temp = new TempDirectory();
        var writer = new TraceArtifactWriter(temp.Path);

        var artifact = CreateRunbookArtifact();

        await writer.WriteAsync(artifact, TestContext.Current.CancellationToken);

        var path = Path.Combine(temp.Path, "artifacts", "traces", "runbook_502_v1", artifact.RunId + ".json");
        var json = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);

        var scenarioResult = doc.RootElement.GetProperty("scenario_result");
        var retrieval = doc.RootElement.GetProperty("retrieval");
        Assert.True(retrieval.TryGetProperty("run1", out var retrievalRun1));
        Assert.True(retrievalRun1.TryGetProperty("candidate_documents", out _));
        Assert.True(retrieval.TryGetProperty("run2", out var retrievalRun2));
        Assert.True(retrievalRun2.TryGetProperty("candidate_documents", out var retrievalRun2CandidateDocuments));
        Assert.Equal(3, retrievalRun2CandidateDocuments.GetArrayLength());
        Assert.True(scenarioResult.TryGetProperty("present_step_labels", out _));
        Assert.True(scenarioResult.TryGetProperty("missing_step_labels", out _));
        Assert.True(scenarioResult.TryGetProperty("order_violation_labels", out _));
        Assert.True(scenarioResult.TryGetProperty("score_breakdown", out var scoreBreakdown));
        Assert.True(scoreBreakdown.TryGetProperty("step_coverage_points", out _));
        Assert.True(scoreBreakdown.TryGetProperty("order_correct_points", out _));
        Assert.True(scoreBreakdown.TryGetProperty("hallucination_penalty", out _));
    }

    [Fact]
    public async Task WriteAsync_WritesRunVerificationEvidenceSchema()
    {
        using var temp = new TempDirectory();
        var writer = new RunVerificationEvidenceWriter(temp.Path);

        var evidence = CreateVerificationEvidence();

        await writer.WriteAsync(evidence, TestContext.Current.CancellationToken);

        var path = Path.Combine(temp.Path, "artifacts", "traces", "policy_refund_v1", evidence.RunId + ".verification.json");
        var json = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("run_id", out _));
        Assert.True(root.TryGetProperty("scenario_id", out _));
        Assert.True(root.TryGetProperty("query", out _));
        Assert.True(root.TryGetProperty("run1", out var run1));
        Assert.True(run1.TryGetProperty("answer", out _));
        Assert.True(run1.TryGetProperty("selected_chunks", out var selectedChunks));
        Assert.True(selectedChunks.GetArrayLength() > 0);

        var firstChunk = selectedChunks[0];
        Assert.True(firstChunk.TryGetProperty("document_id", out _));
        Assert.True(firstChunk.TryGetProperty("chunk_id", out _));
        Assert.True(firstChunk.TryGetProperty("chunk_index", out _));
        Assert.True(firstChunk.TryGetProperty("chunk_text", out _));
    }

    private static TraceArtifact CreatePolicyArtifact()
    {
        return new TraceArtifact(
            "policy_refund_v1_20260310T120000Z_abcd",
            "policy_refund_v1",
            "policy_refund_v1",
            "What is the refund policy for annual subscriptions?",
            "run2",
            "2026-03-10T12:00:00Z",
            new[] { "base query" },
            10,
            new[] { new TraceArtifactSelectedChunk("01", "01_0", 0, "chunk") },
            1200,
            "A. Summary\nanswer",
            60,
            new[] { "query suggestion" },
            60,
            null,
            null,
            Array.Empty<string>(),
            new PolicyRefundScenarioResultPayload(
                new[]
                {
                    Phase4RuleTables.PresentCoolingOffWindow,
                    Phase4RuleTables.PresentAnnualProrationRule
                },
                new[] { "MISSING_COOLING_OFF_WINDOW" },
                Array.Empty<string>(),
                new PolicyRefundScoreBreakdownPayload(40, 20, 0, false)),
            new[]
            {
                new TraceArtifactDetectedEvidenceItem(
                    Phase4RuleTables.PresentAnnualProrationRule,
                    "06",
                    "prorated reimbursement",
                    "Customers may receive prorated reimbursement for unused service value.")
            },
            "\n\nDetected evidence from retrieved context:\n- Document 06 [PRESENT_ANNUAL_PRORATION_RULE]: \"Customers may receive prorated reimbursement for unused service value.\"\nYou must incorporate all detected evidence items above into your answer. Do not contradict or omit any detected evidence.",
            new TraceArtifactRetrieval(
                new TraceArtifactRetrievalRun(new[] { "02", "01", "03" }, Ranked: true),
                new TraceArtifactRetrievalRun(new[] { "02", "06", "05" }, Ranked: true)));
    }

    private static TraceArtifact CreateRunbookArtifact()
    {
        return new TraceArtifact(
            "runbook_502_v1_20260310T120000Z_abcd",
            "runbook_502_v1",
            "runbook_502_v1",
            "The service returns 502. What do I do?",
            "run1",
            "2026-03-10T12:00:00Z",
            new[] { "base query" },
            8,
            new[] { new TraceArtifactSelectedChunk("01", "01_0", 0, "chunk") },
            900,
            "A. Summary\nanswer",
            75,
            Array.Empty<string>(),
            75,
            null,
            null,
            Array.Empty<string>(),
            new Runbook502ScenarioResultPayload(
                new[]
                {
                    Runbook502RuleTables.StepCheckUpstreamHealth,
                    Runbook502RuleTables.StepInspectLogs
                },
                new[] { "STEP_CHECK_LB_LOGS" },
                new[] { "ORDER_VIOLATION" },
                new Runbook502ScoreBreakdownPayload(40, 30, 0)),
            null,
            string.Empty,
            new TraceArtifactRetrieval(
                new TraceArtifactRetrievalRun(new[] { "04", "02", "03" }, Ranked: true),
                new TraceArtifactRetrievalRun(new[] { "04", "02", "03" }, Ranked: true)));
    }

    private static RunVerificationEvidence CreateVerificationEvidence()
    {
        return new RunVerificationEvidence(
            "policy_refund_v1_20260310T120000Z_abcd",
            "policy_refund_v1",
            "What is the refund policy for annual subscriptions?",
            new RunVerificationEvidenceRun(
                "A. Summary\nRun 1 answer",
                new[]
                {
                    new RunVerificationEvidenceSelectedChunk("02", "02_0", 0, "chunk"),
                    new RunVerificationEvidenceSelectedChunk("06", "06_1", 1, "chunk 2")
                }));
    }
}
