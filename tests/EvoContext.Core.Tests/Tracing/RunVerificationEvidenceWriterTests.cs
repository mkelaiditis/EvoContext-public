using System.Text.Json;
using EvoContext.Cli.Services;
using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Models;
using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests.Tracing;

public sealed class RunVerificationEvidenceWriterTests
{
    [Fact]
    public async Task WriteAsync_WritesVerificationEvidenceToScenarioTraceDirectory()
    {
        using var temp = new TempDirectory();
        var writer = new RunVerificationEvidenceWriter(temp.Path);
        var evidence = CreateVerificationEvidence();

        await writer.WriteAsync(evidence, TestContext.Current.CancellationToken);

        var path = Path.Combine(temp.Path, "artifacts", "traces", evidence.ScenarioId, evidence.RunId + ".verification.json");
        Assert.True(File.Exists(path));

        var json = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(evidence.RunId, doc.RootElement.GetProperty("run_id").GetString());
        Assert.Equal(evidence.ScenarioId, doc.RootElement.GetProperty("scenario_id").GetString());
        Assert.Equal(evidence.Query, doc.RootElement.GetProperty("query").GetString());
        Assert.Equal(evidence.Run1.Answer, doc.RootElement.GetProperty("run1").GetProperty("answer").GetString());
        Assert.Equal("02", doc.RootElement.GetProperty("run1").GetProperty("selected_chunks")[0].GetProperty("document_id").GetString());
    }

    [Fact]
    public void BuildRunVerificationEvidence_UsesRun1PayloadAndFinalRunId_WhenRun2Exists()
    {
        var scenario = new ScenarioDefinition(
            "policy_refund_v1",
            "Policy Refund",
            "data/scenarios/policy_refund_v1/documents",
            "What is the refund policy for annual subscriptions?",
            Array.Empty<string>(),
            "run2",
            "Policy refund");
        var run = CreateExecutionRun(includeRun2: true);

        var evidence = TraceArtifactBuilder.BuildRunVerificationEvidence(scenario, scenario.PrimaryQuery, run);

        Assert.Equal(run.Run2Result!.RunId, evidence.RunId);
        Assert.Equal(run.Run1Result.Answer, evidence.Run1.Answer);
        Assert.Equal(new[] { "02", "06" }, evidence.Run1.SelectedChunks.Select(chunk => chunk.DocumentId).ToArray());
    }

    [Fact]
    public void BuildTraceArtifact_IncludesDetectedEvidenceAndEvidenceBlockFromFinalRun()
    {
        var scenario = new ScenarioDefinition(
            "policy_refund_v1",
            "Policy Refund",
            "data/scenarios/policy_refund_v1/documents",
            "What is the refund policy for annual subscriptions?",
            Array.Empty<string>(),
            "run2",
            "Policy refund");
        var run = CreateExecutionRun(includeRun2: true);

        var artifact = TraceArtifactBuilder.BuildTraceArtifact(scenario, scenario.PrimaryQuery, run);

        var evidenceItem = Assert.Single(artifact.DetectedEvidenceItems!);
        Assert.Equal(Phase4RuleTables.PresentAnnualProrationRule, evidenceItem.FactLabel);
        Assert.Equal("06", evidenceItem.DocumentId);
        Assert.Equal("prorated reimbursement", evidenceItem.MatchedAnchor);
        Assert.Equal("Customers may receive prorated reimbursement for unused service value.", evidenceItem.ExtractedSnippet);
        Assert.Contains("Detected evidence from retrieved context:", artifact.EvidenceBlock, StringComparison.Ordinal);
    }

    private static RunVerificationEvidence CreateVerificationEvidence()
    {
        return new RunVerificationEvidence(
            "policy_refund_v1_20260313T010203Z_abcd",
            "policy_refund_v1",
            "What is the refund policy for annual subscriptions?",
            new RunVerificationEvidenceRun(
                "Run 1 answer",
                [
                    new RunVerificationEvidenceSelectedChunk("02", "02_0", 0, "chunk text"),
                    new RunVerificationEvidenceSelectedChunk("06", "06_1", 1, "chunk text 2")
                ]));
    }

    private static Run5ExecutionRun CreateExecutionRun(bool includeRun2)
    {
        var query = "What is the refund policy for annual subscriptions?";
        var run1Id = "policy_refund_v1_20990101T000000Z_run1";
        var run1Request = new RunRequest("policy_refund_v1", query, RunMode.Run1AnswerGeneration);
        var run1Selected = new[]
        {
            new RetrievalCandidate("q", 1, 0.90f, 0.90f, "02", "02_0", 0, "cooling off chunk"),
            new RetrievalCandidate("q", 2, 0.80f, 0.80f, "06", "06_1", 1, "proration chunk")
        };
        var run1Summary = new RetrievalSummary(
            run1Selected,
            run1Selected,
            new EvoContext.Core.Context.ContextPack("run1 context", 100, 2, 2200));
        var run1Result = new RunResult(run1Id, run1Request, run1Summary, "Run 1 answer", null);
        var scoreBreakdown = new ScoreBreakdown(40, 20, 0, false);
        var run1ScenarioResult = new PolicyRefundScenarioResult(
            new[] { Phase4RuleTables.PresentCoolingOffWindow },
            new[] { "MISSING_ANNUAL_PRORATION_RULE" },
            Array.Empty<string>(),
            scoreBreakdown);
        var run1Evaluation = new EvaluationResult(run1Id, "policy_refund_v1", 60, Array.Empty<string>(), run1ScenarioResult);
        var run1Feedback = new FeedbackOutput(run1Id, "policy_refund_v1", 60, scoreBreakdown, new[] { "MISSING_ANNUAL_PRORATION_RULE" }, Array.Empty<string>(), new[] { "early termination prorated reimbursement" });

        if (!includeRun2)
        {
            return new Run5ExecutionRun(
                run1Result,
                run1Evaluation,
                run1Feedback,
                new Run2Trigger(60, new[] { "MISSING_ANNUAL_PRORATION_RULE" }, false),
                null,
                null,
                null,
                null,
                new RunScoreDelta(60, 60, 0),
                0,
                Array.Empty<TraceEvent>());
        }

        var run2Id = "policy_refund_v1_20990101T000000Z_run2";
        var run2Request = new RunRequest("policy_refund_v1", query, RunMode.Run2FeedbackExpanded);
        var run2Selected = new[]
        {
            new RetrievalCandidate("q", 1, 0.95f, 0.95f, "06", "06_1", 1, "proration chunk"),
            new RetrievalCandidate("q", 2, 0.85f, 0.85f, "05", "05_0", 0, "billing chunk")
        };
        var run2Summary = new RetrievalSummary(
            run2Selected,
            run2Selected,
            new EvoContext.Core.Context.ContextPack("run2 context", 120, 2, 2200));
        var run2Result = new RunResult(
            run2Id,
            run2Request,
            run2Summary,
            "Run 2 answer",
            null,
            new[]
            {
                new EvoContext.Core.Evidence.DetectedEvidenceItem(
                    "06",
                    "F2",
                    Phase4RuleTables.PresentAnnualProrationRule,
                    "prorated reimbursement",
                    "Customers may receive prorated reimbursement for unused service value.")
            },
            "\n\nDetected evidence from retrieved context:\n- Document 06 [PRESENT_ANNUAL_PRORATION_RULE]: \"Customers may receive prorated reimbursement for unused service value.\"\nYou must incorporate all detected evidence items above into your answer. Do not contradict or omit any detected evidence.");
        var run2ScenarioResult = new PolicyRefundScenarioResult(
            new[] { Phase4RuleTables.PresentAnnualProrationRule },
            Array.Empty<string>(),
            Array.Empty<string>(),
            scoreBreakdown);
        var run2Evaluation = new EvaluationResult(run2Id, "policy_refund_v1", 85, Array.Empty<string>(), run2ScenarioResult);
        var run2Feedback = new FeedbackOutput(run2Id, "policy_refund_v1", 85, scoreBreakdown, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        return new Run5ExecutionRun(
            run1Result,
            run1Evaluation,
            run1Feedback,
            new Run2Trigger(60, new[] { "MISSING_ANNUAL_PRORATION_RULE" }, true),
            new RetrievalQuerySet(query, new[] { "early termination prorated reimbursement" }, new[] { query, "early termination prorated reimbursement" }),
            run2Result,
            run2Evaluation,
            run2Feedback,
            new RunScoreDelta(60, 85, 25),
            2,
            Array.Empty<TraceEvent>());
    }
}