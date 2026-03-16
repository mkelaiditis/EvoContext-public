using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Core.Evaluation;

public sealed record Runbook502ScoreComputationResult(
    int ScoreTotal,
    Runbook502ScoreBreakdown Breakdown);

public sealed class Runbook502ScoreCalculator
{
    private readonly ILogger _logger;

    public Runbook502ScoreCalculator(ILogger? logger = null)
    {
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<Runbook502ScoreCalculator>();
    }

    public Runbook502ScoreComputationResult Compute(
        int presentStepCount,
        int detectedStepCount,
        bool hasOrderViolations,
        bool hasListFormat,
        int hallucinationPenalty)
    {
        const int AccuracyBase = 8;
        const int StepPoints = 18;
        var stepCoveragePoints = detectedStepCount * StepPoints;
        var formatPoints = hasListFormat ? 10 : 0;

        var total = AccuracyBase + stepCoveragePoints + formatPoints - hallucinationPenalty;
        total = Math.Clamp(total, 0, 100);

        _logger
            .WithProperties(
                ("present_step_count", presentStepCount),
                ("detected_step_count", detectedStepCount),
                ("has_order_violations", hasOrderViolations),
                ("has_list_format", hasListFormat),
                ("hallucination_penalty", hallucinationPenalty),
                ("step_coverage_points", stepCoveragePoints),
                ("format_points", formatPoints),
                ("score_total", total))
            .Debug("Runbook 502 score computed");

        return new Runbook502ScoreComputationResult(
            total,
            new Runbook502ScoreBreakdown(
                stepCoveragePoints,
                0,
                hallucinationPenalty));
    }
}
