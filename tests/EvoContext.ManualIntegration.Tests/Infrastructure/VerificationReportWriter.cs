using System.Text;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal sealed class VerificationReportWriter
{
    public string Write(VerificationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var lines = new List<string>
        {
            $"scenario_id={report.VerificationCase.ScenarioId}",
            $"query={report.VerificationCase.QueryText}",
            $"run_mode={report.VerificationCase.RunMode}"
        };

        if (report.PreparationResult is not null)
        {
            lines.Add($"preparation_step={report.PreparationResult.StepName}");
            lines.Add($"preparation_status={report.PreparationResult.Status}");
            lines.Add($"preparation_exit_code={report.PreparationResult.ExitCode?.ToString() ?? "<none>"}");
        }

        if (report.ExecutionResult is not null)
        {
            lines.Add($"execution_step={report.ExecutionResult.StepName}");
            lines.Add($"execution_status={report.ExecutionResult.Status}");
            lines.Add($"execution_exit_code={report.ExecutionResult.ExitCode?.ToString() ?? "<none>"}");
        }

        if (!string.IsNullOrWhiteSpace(report.ArtifactPaths.RunId))
        {
            lines.Add($"run_id={report.ArtifactPaths.RunId}");
        }

        if (!string.IsNullOrWhiteSpace(report.ArtifactPaths.TraceArtifactPath))
        {
            lines.Add($"trace_artifact={report.ArtifactPaths.TraceArtifactPath}");
        }

        if (!string.IsNullOrWhiteSpace(report.ArtifactPaths.VerificationEvidencePath))
        {
            lines.Add($"verification_evidence={report.ArtifactPaths.VerificationEvidencePath}");
        }

        if (report.Run2Validation is not null)
        {
            lines.Add($"score_run1={report.Run2Validation.ScoreRun1}");
            lines.Add($"score_run2={report.Run2Validation.ScoreRun2?.ToString() ?? "<missing>"}");
            lines.Add($"score_delta={report.Run2Validation.ScoreDelta?.ToString() ?? "<missing>"}");
            lines.Add($"run2_selected_documents={JoinValues(report.Run2Validation.Run2SelectedChunkDocumentIds)}");
            lines.Add($"run2_document_06_found={report.Run2Validation.Run2Document06Found}");
            lines.Add($"proration_evidence_detected={report.Run2Validation.ProrationEvidenceDetected}");
            lines.Add($"proration_matches={JoinValues(report.Run2Validation.ProrationMatches)}");

            if (report.Run2Validation.CoolingOffSignalPresent.HasValue)
            {
                lines.Add($"run1_cooling_off_signal_present={report.Run2Validation.CoolingOffSignalPresent.Value}");
            }

            if (report.Run2Validation.Run1ProrationAbsent.HasValue)
            {
                lines.Add($"run1_proration_absent={report.Run2Validation.Run1ProrationAbsent.Value}");
            }
        }

        if (report.Run1Evidence is not null)
        {
            lines.Add($"run1_selected_documents={JoinValues(report.Run1Evidence.SelectedChunkDocumentIds)}");
        }

        foreach (var (label, jsonPath) in report.FieldPaths.Snapshot().OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            lines.Add($"field_path.{label}={jsonPath}");
        }

        if (!string.IsNullOrWhiteSpace(report.FailurePhase))
        {
            lines.Add($"failure_phase={report.FailurePhase}");
        }

        if (report.FinalStatus == VerificationFinalStatus.Failed)
        {
            if (report.Run1Evidence is not null)
            {
                lines.Add($"run1_answer={NormalizeValue(report.Run1Evidence.Answer)}");
            }

            if (report.Run2Validation is not null)
            {
                lines.Add($"run2_answer={NormalizeValue(report.Run2Validation.Run2Answer)}");
            }
        }

        if (report.FailedConditions.Count > 0)
        {
            lines.Add($"failed_conditions={JoinValues(report.FailedConditions)}");
        }

        lines.Add($"final_status={report.FinalStatus}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string JoinValues(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "<none>" : string.Join(",", values);
    }

    private static string NormalizeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Trim();
    }
}