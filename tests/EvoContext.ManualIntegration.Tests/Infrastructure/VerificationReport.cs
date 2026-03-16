using System;
using System.Collections.Generic;
using System.Linq;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal enum VerificationFinalStatus
{
    NotStarted,
    Passed,
    Failed
}

internal sealed record ManualIntegrationVerificationCase(
    string ScenarioId,
    string QueryText,
    string RunMode,
    string CategoryTrait,
    string CliProjectPath,
    TimeSpan StepTimeout,
    TimeSpan TotalTimeout,
    IReadOnlyList<string> RequiredEnvironmentKeys);

internal sealed record VerificationArtifactPaths(
    string? RunId,
    string? TraceArtifactPath,
    string? OperationalTracePath,
    string? VerificationEvidencePath)
{
    public static VerificationArtifactPaths Empty { get; } = new(null, null, null, null);
}

internal sealed record VerificationPhaseResult(
    string PhaseName,
    CliStepName StepName,
    CliStepStatus Status,
    int? ExitCode,
    TimeSpan? Duration,
    bool CombinedPreparationMember);

internal sealed class VerificationReport
{
    private readonly List<CliStepExecution> _steps = [];
    private readonly List<string> _failedConditions = [];

    public VerificationReport(ManualIntegrationVerificationCase verificationCase, string collectionName, FieldPathRegistry? fieldPaths = null)
    {
        VerificationCase = verificationCase ?? throw new ArgumentNullException(nameof(verificationCase));
        CollectionName = string.IsNullOrWhiteSpace(collectionName)
            ? throw new ArgumentException("Collection name is required.", nameof(collectionName))
            : collectionName;
        FieldPaths = fieldPaths ?? new FieldPathRegistry();
        ArtifactPaths = VerificationArtifactPaths.Empty;
    }

    public ManualIntegrationVerificationCase VerificationCase { get; }

    public string CollectionName { get; }

    public EnvironmentPrerequisiteResult? Prerequisites { get; private set; }

    public FieldPathRegistry FieldPaths { get; }

    public VerificationArtifactPaths ArtifactPaths { get; private set; }

    public string? FailurePhase { get; private set; }

    public VerificationFinalStatus FinalStatus { get; private set; } = VerificationFinalStatus.NotStarted;

    public VerificationPhaseResult? PreparationResult { get; private set; }

    public VerificationPhaseResult? ExecutionResult { get; private set; }

    public Run2ValidationResult? Run2Validation { get; private set; }

    public Run1VerificationEvidence? Run1Evidence { get; private set; }

    public IReadOnlyList<CliStepExecution> Steps => _steps;

    public IReadOnlyList<string> FailedConditions => _failedConditions;

    public void SetPrerequisites(EnvironmentPrerequisiteResult prerequisites)
    {
        Prerequisites = prerequisites ?? throw new ArgumentNullException(nameof(prerequisites));
        if (!prerequisites.IsSatisfied)
        {
            MarkFailed(
                "prerequisites",
                $"Missing required environment keys: {string.Join(", ", prerequisites.MissingRequiredKeys)}");
        }
    }

    public void AddStep(CliStepExecution stepExecution)
    {
        ArgumentNullException.ThrowIfNull(stepExecution);
        _steps.Add(stepExecution);

        var phaseName = stepExecution.CombinedPreparationMember ? "preparation" : "execution";
        var phaseResult = new VerificationPhaseResult(
            phaseName,
            stepExecution.StepName,
            stepExecution.Status,
            stepExecution.ExitCode,
            stepExecution.Duration,
            stepExecution.CombinedPreparationMember);

        if (stepExecution.CombinedPreparationMember)
        {
            PreparationResult = phaseResult;
        }
        else
        {
            ExecutionResult = phaseResult;
        }

        if (stepExecution.Succeeded)
        {
            return;
        }

        var failureReason = stepExecution.Status == CliStepStatus.TimedOut
            ? $"Step '{stepExecution.StepName}' timed out after {stepExecution.TimeoutBudget.TotalMinutes:0.#} minute(s)."
            : $"Step '{stepExecution.StepName}' failed with exit code {stepExecution.ExitCode?.ToString() ?? "<unknown>"}.";

        MarkFailed(phaseName, failureReason);
    }

    public CliStepExecution? FindStep(CliStepName stepName)
    {
        return _steps.LastOrDefault(step => step.StepName == stepName);
    }

    public void SetArtifacts(VerificationArtifactDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArtifactPaths = new VerificationArtifactPaths(
            discovery.RunId,
            discovery.TraceArtifactPath,
            discovery.OperationalTracePath,
            discovery.VerificationEvidencePath);
    }

    public void SetRun1Evidence(Run1VerificationEvidence run1Evidence)
    {
        Run1Evidence = run1Evidence ?? throw new ArgumentNullException(nameof(run1Evidence));
    }

    public void RecordPhaseTimeout(CliStepName stepName, bool combinedPreparationMember, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var phaseResult = new VerificationPhaseResult(
            combinedPreparationMember ? "preparation" : "execution",
            stepName,
            CliStepStatus.TimedOut,
            null,
            null,
            combinedPreparationMember);

        if (combinedPreparationMember)
        {
            PreparationResult = phaseResult;
        }
        else
        {
            ExecutionResult = phaseResult;
        }

        MarkFailed(phaseResult.PhaseName, reason);
    }

    public void SetRun2Validation(Run2ValidationResult validationResult)
    {
        Run2Validation = validationResult ?? throw new ArgumentNullException(nameof(validationResult));

        foreach (var failedCondition in validationResult.FailedConditions)
        {
            MarkFailed("validation", failedCondition);
        }
    }

    public void MarkFailed(string phase, string failedCondition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        ArgumentException.ThrowIfNullOrWhiteSpace(failedCondition);

        FailurePhase ??= phase;
        FinalStatus = VerificationFinalStatus.Failed;

        if (!_failedConditions.Contains(failedCondition, StringComparer.Ordinal))
        {
            _failedConditions.Add(failedCondition);
        }
    }

    public void MarkPassed()
    {
        FinalStatus = VerificationFinalStatus.Passed;
    }

    public string BuildSummary()
    {
        return new VerificationReportWriter().Write(this);
    }
}