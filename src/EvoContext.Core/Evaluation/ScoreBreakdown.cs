namespace EvoContext.Core.Evaluation;

public sealed record ScoreBreakdown(
    int CompletenessPoints,
    int FormatPoints,
    int HallucinationPenalty,
    bool AccuracyCapApplied);
