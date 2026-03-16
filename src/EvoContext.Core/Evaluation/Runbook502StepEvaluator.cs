using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Core.Evaluation;

public sealed record Runbook502StepEvaluationResult(
    IReadOnlyList<string> PresentStepLabels,
    IReadOnlyList<string> MissingStepLabels,
    IReadOnlyList<string> OrderViolationLabels,
    int StepCoveragePoints);

public sealed class Runbook502StepEvaluator
{
    private static readonly IReadOnlyList<NormalizedRunbook502StepRule> NormalizedRules = Runbook502RuleTables.StepRules
        .Select(rule => new NormalizedRunbook502StepRule(
            rule.StepLabel,
            Runbook502PatternMatcher.NormalizePatterns(rule.DetectionPatterns),
            Runbook502PatternMatcher.NormalizePatterns(rule.ContextAnchors)))
        .ToList();
    private readonly ILogger _logger;

    public Runbook502StepEvaluator(ILogger? logger = null)
    {
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<Runbook502StepEvaluator>();
    }

    public Runbook502StepEvaluationResult Evaluate(string answerText, string contextText)
    {
        var normalizedAnswer = Phase4TextNormalizer.Normalize(answerText);
        var normalizedContext = Phase4TextNormalizer.Normalize(contextText);
        return EvaluateNormalized(normalizedAnswer, normalizedContext);
    }

    public Runbook502StepEvaluationResult EvaluateNormalized(string normalizedAnswer, string normalizedContext)
    {
        var present = new List<string>();
        var missing = new List<string>();
        var firstPositions = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var rule in NormalizedRules)
        {
            var answerHasPattern = Runbook502PatternMatcher.ContainsAny(normalizedAnswer, rule.DetectionPatterns);
            var contextHasAnchor = Runbook502PatternMatcher.ContainsAny(normalizedContext, rule.ContextAnchors);

            if (answerHasPattern && contextHasAnchor)
            {
                present.Add(rule.StepLabel);
                firstPositions[rule.StepLabel] = Runbook502PatternMatcher.FirstIndexOfAny(
                    normalizedAnswer,
                    rule.DetectionPatterns);
            }
            else
            {
                missing.Add(rule.StepLabel);
            }

            _logger
                .WithProperties(
                    ("step_label", rule.StepLabel),
                    ("answer_pattern_match", answerHasPattern),
                    ("context_anchor_match", contextHasAnchor),
                    ("is_present", answerHasPattern && contextHasAnchor))
                .Debug("Runbook 502 step evaluated");
        }

        var orderViolations = DetectOrderViolations(firstPositions);
        var coveragePoints = present.Count * 10;

        _logger
            .WithProperties(
                ("present_step_count", present.Count),
                ("missing_step_count", missing.Count),
                ("order_violation_count", orderViolations.Count),
                ("step_coverage_points", coveragePoints))
            .Debug("Runbook 502 step evaluation completed");

        return new Runbook502StepEvaluationResult(
            present,
            missing,
            orderViolations,
            coveragePoints);
    }

    private static IReadOnlyList<string> DetectOrderViolations(IReadOnlyDictionary<string, int> firstPositions)
    {
        if (firstPositions.Count < 2)
        {
            return Array.Empty<string>();
        }

        var violations = new List<string>();
        var orderedSteps = Runbook502RuleTables.RequiredStepOrder;

        for (var i = 0; i < orderedSteps.Count - 1; i++)
        {
            var stepA = orderedSteps[i];
            if (!firstPositions.TryGetValue(stepA, out var indexA))
            {
                continue;
            }

            for (var j = i + 1; j < orderedSteps.Count; j++)
            {
                var stepB = orderedSteps[j];
                if (!firstPositions.TryGetValue(stepB, out var indexB))
                {
                    continue;
                }

                if (indexB < indexA)
                {
                    violations.Add($"ORDER_{stepB}_BEFORE_{stepA}");
                }
            }
        }

        return violations;
    }

    private sealed record NormalizedRunbook502StepRule(
        string StepLabel,
        IReadOnlyList<string> DetectionPatterns,
        IReadOnlyList<string> ContextAnchors);
}
