using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Core.Evaluation;

public sealed record FactEvaluationResult(
    IReadOnlyList<string> PresentFactIds,
    IReadOnlyList<string> MissingLabels,
    int CompletenessPoints,
    bool UngroundedF2Detected);

public sealed class Phase4FactEvaluator
{
    private const string FactIdF2 = "F2";
    private static readonly IReadOnlyList<NormalizedFactRule> NormalizedRules = Phase4RuleTables.FactRules
        .Select(rule => new NormalizedFactRule(
            rule.FactId,
            rule.MissingLabel,
            Phase4PatternMatcher.NormalizePatterns(rule.AnswerPatterns),
            Phase4PatternMatcher.NormalizePatterns(rule.ContextAnchors),
            rule.RequiresDualAnswerMatch,
            Phase4PatternMatcher.NormalizePatterns(rule.SecondaryAnswerPatterns),
            Phase4PatternMatcher.NormalizePatterns(rule.NegationGuardPatterns)))
        .ToList();
    private readonly ILogger _logger;

    public Phase4FactEvaluator(ILogger? logger = null)
    {
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<Phase4FactEvaluator>();
    }

    public FactEvaluationResult Evaluate(string answerText, string contextText)
    {
        var normalizedAnswer = Phase4TextNormalizer.Normalize(answerText);
        var normalizedContext = Phase4TextNormalizer.Normalize(contextText);

        return EvaluateNormalized(normalizedAnswer, normalizedContext);
    }

    public FactEvaluationResult EvaluateNormalized(string normalizedAnswer, string normalizedContext)
    {
        var presentFacts = new List<string>();
        var missingLabels = new List<string>();
        var ungroundedF2 = false;

        foreach (var rule in NormalizedRules)
        {
            var answerHasPrimary = Phase4PatternMatcher.ContainsAnyAffirmative(
                normalizedAnswer,
                rule.AnswerPatterns,
                rule.NegationGuardPatterns);
            var answerHasSecondary = rule.RequiresDualAnswerMatch
                ? Phase4PatternMatcher.ContainsAny(normalizedAnswer, rule.SecondaryAnswerPatterns)
                : true;
            var answerHasRequired = answerHasPrimary && answerHasSecondary;
            var contextHasAnchor = Phase4PatternMatcher.ContainsAny(normalizedContext, rule.ContextAnchors);
            var isPresent = answerHasRequired && contextHasAnchor;

            if (isPresent)
            {
                presentFacts.Add(rule.FactId);
            }
            else
            {
                missingLabels.Add(rule.MissingLabel);
            }

            if (rule.FactId == FactIdF2 && answerHasRequired && !contextHasAnchor)
            {
                ungroundedF2 = true;
            }

            _logger
                .WithProperties(
                    ("fact_id", rule.FactId),
                    ("missing_label", rule.MissingLabel),
                    ("answer_primary_match", answerHasPrimary),
                    ("answer_secondary_match", answerHasSecondary),
                    ("context_anchor_match", contextHasAnchor),
                    ("is_present", isPresent))
                .Debug("Phase 4 fact evaluated");
        }

        var completenessPoints = presentFacts.Count * Phase4Constants.FactPointsPerPresent;

        return new FactEvaluationResult(presentFacts, missingLabels, completenessPoints, ungroundedF2);
    }

    private sealed record NormalizedFactRule(
        string FactId,
        string MissingLabel,
        IReadOnlyList<string> AnswerPatterns,
        IReadOnlyList<string> ContextAnchors,
        bool RequiresDualAnswerMatch,
        IReadOnlyList<string> SecondaryAnswerPatterns,
        IReadOnlyList<string> NegationGuardPatterns);
}
