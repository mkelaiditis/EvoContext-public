using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Core.Evaluation;

public sealed record HallucinationDetectionResult(
    IReadOnlyList<string> HallucinationFlags,
    int HallucinationPenalty);

public sealed class Phase4HallucinationDetector
{
    private static readonly IReadOnlyList<NormalizedHallucinationRule> NormalizedRules = Phase4RuleTables.HallucinationRules
        .Select(rule => new NormalizedHallucinationRule(
            rule.Flag,
            Phase4PatternMatcher.NormalizePatterns(rule.Patterns)))
        .ToList();
    private readonly ILogger _logger;

    public Phase4HallucinationDetector(ILogger? logger = null)
    {
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<Phase4HallucinationDetector>();
    }

    public HallucinationDetectionResult Evaluate(string answerText)
    {
        var normalizedAnswer = Phase4TextNormalizer.Normalize(answerText);
        return EvaluateNormalized(normalizedAnswer);
    }

    public HallucinationDetectionResult EvaluateNormalized(string normalizedAnswer)
    {
        var flags = new List<string>();

        foreach (var rule in NormalizedRules)
        {
            var isDetected = Phase4PatternMatcher.ContainsAny(normalizedAnswer, rule.Patterns);
            if (isDetected)
            {
                flags.Add(rule.Flag);
            }

            _logger
                .WithProperties(
                    ("hallucination_flag", rule.Flag),
                    ("is_detected", isDetected))
                .Debug("Phase 4 hallucination rule evaluated");
        }

        var penalty = CalculatePenalty(flags.Count);

        _logger
            .WithProperties(
                ("flag_count", flags.Count),
                ("hallucination_penalty", penalty))
            .Debug("Phase 4 hallucination evaluated");

        return new HallucinationDetectionResult(flags, penalty);
    }

    private static int CalculatePenalty(int flagCount)
    {
        if (flagCount <= 0)
        {
            return 0;
        }

        var penalty = flagCount * Phase4Constants.HallucinationPenaltyPerFlag;
        return Math.Max(Phase4Constants.HallucinationPenaltyCap, penalty);
    }

    private sealed record NormalizedHallucinationRule(
        string Flag,
        IReadOnlyList<string> Patterns);
}
