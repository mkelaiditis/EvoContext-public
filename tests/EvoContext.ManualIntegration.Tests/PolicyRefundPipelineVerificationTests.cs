using System.Threading.Tasks;
using EvoContext.ManualIntegration.Tests.Infrastructure;

namespace EvoContext.ManualIntegration.Tests;

public sealed class PolicyRefundPipelineVerificationTests
{
    [Fact]
    [Trait("Category", PolicyRefundVerificationHarness.CategoryTrait)]
    public async Task PolicyRefundPipelineVerification_CompletesEmbedAndRunWorkflow()
    {
        const string queryText = "What is the refund policy for annual subscriptions?";
        var harness = new PolicyRefundVerificationHarness(queryText);

        var report = await harness.ExecuteAsync(TestContext.Current.CancellationToken);
        var summary = report.BuildSummary();

        Assert.True(report.PreparationResult is not null, summary);
        Assert.True(report.PreparationResult!.StepName == CliStepName.Embed, summary);
        Assert.True(report.PreparationResult.Status == CliStepStatus.Succeeded, summary);

        Assert.True(report.ExecutionResult is not null, summary);
        Assert.True(report.ExecutionResult!.StepName == CliStepName.Run, summary);
        Assert.True(report.ExecutionResult.Status == CliStepStatus.Succeeded, summary);

        Assert.True(report.Steps.Count == 2, summary);
        Assert.True(!string.IsNullOrWhiteSpace(report.ArtifactPaths.RunId), summary);
        Assert.True(!string.IsNullOrWhiteSpace(report.ArtifactPaths.TraceArtifactPath), summary);
        Assert.True(!string.IsNullOrWhiteSpace(report.ArtifactPaths.VerificationEvidencePath), summary);
        Assert.True(report.Run1Evidence is not null, summary);
        Assert.True(report.Run1Evidence!.SelectedChunkDocumentIds.Count > 0, summary);
        Assert.True(report.Run2Validation is not null, summary);
        Assert.True(report.Run2Validation!.ScoresPresent, summary);
        Assert.True(report.Run2Validation.ScoreRun2.HasValue, summary);
        Assert.True(report.Run2Validation.ScoreDelta.HasValue, summary);
        Assert.True(report.Run2Validation.ScoreDeltaOk, summary);
        Assert.True(report.Run2Validation.Run2Document06Found, summary);
        Assert.True(report.Run2Validation.ProrationEvidenceDetected, summary);
        Assert.True(report.FieldPaths.TryGet("run1_answer", out var run1AnswerPath), summary);
        Assert.True(run1AnswerPath == "$.run1.answer", summary);
        Assert.True(report.FieldPaths.TryGet("run1_selected_chunk_document_ids", out var run1SelectedPath), summary);
        Assert.True(run1SelectedPath == "$.run1.selected_chunks[*].document_id", summary);
        Assert.True(report.FieldPaths.TryGet("score_run1", out var scoreRun1Path), summary);
        Assert.True(scoreRun1Path == "$.score_run1", summary);
        Assert.True(report.FieldPaths.TryGet("score_run2", out var scoreRun2Path), summary);
        Assert.True(scoreRun2Path == "$.score_run2", summary);
        Assert.True(report.FieldPaths.TryGet("score_delta", out var scoreDeltaPath), summary);
        Assert.True(scoreDeltaPath == "$.score_delta", summary);
        Assert.True(report.FieldPaths.TryGet("run2_selected_chunk_document_ids", out var run2SelectedPath), summary);
        Assert.True(run2SelectedPath == "$.selected_chunks[*].document_id", summary);
        Assert.True(report.FieldPaths.TryGet("run2_answer", out var run2AnswerPath), summary);
        Assert.True(run2AnswerPath == "$.answer", summary);
        Assert.Contains("run1_selected_documents=", summary);
        Assert.Contains("run2_selected_documents=", summary);
        Assert.Contains("field_path.run1_answer=$.run1.answer", summary);
        Assert.Contains("field_path.score_run1=$.score_run1", summary);

        if (report.FinalStatus == VerificationFinalStatus.Failed)
        {
            Assert.Contains("run1_answer=", summary);
            Assert.Contains("run2_answer=", summary);
            Assert.Contains("failed_conditions=", summary);
        }

        Assert.True(report.FailurePhase is null, summary);
        Assert.True(report.FinalStatus == VerificationFinalStatus.Passed, summary);
    }
}