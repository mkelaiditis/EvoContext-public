namespace EvoContext.Core.Evaluation;

public interface IScenarioResult
{
}

public sealed record PolicyRefundScenarioResult(
    IReadOnlyList<string> PresentFactLabels,
    IReadOnlyList<string> MissingFactLabels,
    IReadOnlyList<string> HallucinationFlags,
    ScoreBreakdown ScoreBreakdown)
    : IScenarioResult;

public sealed record Runbook502ScenarioResult(
    IReadOnlyList<string> PresentStepLabels,
    IReadOnlyList<string> MissingStepLabels,
    IReadOnlyList<string> OrderViolationLabels,
    Runbook502ScoreBreakdown ScoreBreakdown)
    : IScenarioResult;

public sealed record Runbook502ScoreBreakdown(
    int StepCoveragePoints,
    int OrderCorrectPoints,
    int HallucinationPenalty);
