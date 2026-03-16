using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Core.Evaluation;

public sealed record Runbook502HallucinationDetectionResult(
    IReadOnlyList<string> HallucinationFlags,
    int HallucinationPenalty);

public sealed class Runbook502HallucinationDetector
{
    private static readonly IReadOnlyList<NormalizedRunbook502HallucinationRule> NormalizedRules = Runbook502RuleTables.HallucinationRules
        .Select(rule => new NormalizedRunbook502HallucinationRule(
            rule.Flag,
            Runbook502PatternMatcher.NormalizePatterns(new[] { rule.Pattern })[0],
            rule.ContextSuppressTerm is null
                ? null
                : Phase4TextNormalizer.Normalize(rule.ContextSuppressTerm)))
        .ToList();
    private readonly ILogger _logger;

    public Runbook502HallucinationDetector(ILogger? logger = null)
    {
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<Runbook502HallucinationDetector>();
    }

    public Runbook502HallucinationDetectionResult Evaluate(string answerText, string contextText)
    {
        var normalizedAnswer = Phase4TextNormalizer.Normalize(answerText);
        var normalizedContext = Phase4TextNormalizer.Normalize(contextText);
        return EvaluateNormalized(normalizedAnswer, normalizedContext);
    }

    public Runbook502HallucinationDetectionResult EvaluateNormalized(string normalizedAnswer, string normalizedContext)
    {
        var flags = new List<string>();

        foreach (var rule in NormalizedRules)
        {
            var answerHasPattern = Runbook502PatternMatcher.ContainsAny(normalizedAnswer, new[] { rule.Pattern });
            var suppressed = !string.IsNullOrWhiteSpace(rule.ContextSuppressTerm)
                && normalizedContext.Contains(rule.ContextSuppressTerm, StringComparison.Ordinal);

            if (!answerHasPattern)
            {
                _logger
                    .WithProperties(
                        ("hallucination_flag", rule.Flag),
                        ("answer_pattern_match", false),
                        ("suppressed_by_context", false),
                        ("is_detected", false))
                    .Debug("Runbook 502 hallucination rule evaluated");
                continue;
            }

            if (suppressed)
            {
                _logger
                    .WithProperties(
                        ("hallucination_flag", rule.Flag),
                        ("answer_pattern_match", true),
                        ("suppressed_by_context", true),
                        ("is_detected", false))
                    .Debug("Runbook 502 hallucination rule evaluated");
                continue;
            }

            flags.Add(rule.Flag);

            _logger
                .WithProperties(
                    ("hallucination_flag", rule.Flag),
                    ("answer_pattern_match", true),
                    ("suppressed_by_context", false),
                    ("is_detected", true))
                .Debug("Runbook 502 hallucination rule evaluated");
        }

        var penalty = Math.Min(flags.Count * 20, 40);

        _logger
            .WithProperties(
                ("flag_count", flags.Count),
                ("hallucination_penalty", penalty))
            .Debug("Runbook 502 hallucination evaluated");

        return new Runbook502HallucinationDetectionResult(flags, penalty);
    }

    private sealed record NormalizedRunbook502HallucinationRule(
        string Flag,
        string Pattern,
        string? ContextSuppressTerm);
}
