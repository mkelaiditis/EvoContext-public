using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Core.Evaluation;

public sealed record ScoreComputationResult(
    int ScoreTotal,
    ScoreBreakdown Breakdown);

public sealed class Phase4ScoreCalculator
{
    private readonly ILogger _logger;

    public Phase4ScoreCalculator(ILogger? logger = null)
    {
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<Phase4ScoreCalculator>();
    }

    public ScoreComputationResult Compute(
        int completenessPoints,
        int formatPoints,
        int hallucinationPenalty,
        bool contradictionDetected)
    {
        var accuracyPoints = Phase4Constants.AccuracyPoints;
        var total = completenessPoints + accuracyPoints + formatPoints + hallucinationPenalty;
        var preClampTotal = total;
        var accuracyCapApplied = false;

        if (contradictionDetected)
        {
            total = Math.Min(total, Phase4Constants.ContradictionScoreCap);
            accuracyCapApplied = true;
        }

        total = Math.Clamp(total, 0, 100);

        _logger
            .WithProperties(
                ("completeness_points", completenessPoints),
                ("accuracy_points", accuracyPoints),
                ("format_points", formatPoints),
                ("hallucination_penalty", hallucinationPenalty),
                ("contradiction_detected", contradictionDetected),
                ("pre_clamp_total", preClampTotal),
                ("score_total", total),
                ("accuracy_cap_applied", accuracyCapApplied))
            .Debug("Phase 4 score computed");

        var breakdown = new ScoreBreakdown(
            completenessPoints,
            formatPoints,
            hallucinationPenalty,
            accuracyCapApplied);

        return new ScoreComputationResult(total, breakdown);
    }
}
