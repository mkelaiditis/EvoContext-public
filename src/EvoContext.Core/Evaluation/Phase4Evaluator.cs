using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Core.Evaluation;

public sealed class Phase4Evaluator
{
    private static readonly IReadOnlyList<string> NormalizedContradictionPatterns = Phase4PatternMatcher
        .NormalizePatterns(Phase4RuleTables.ContradictionPatterns);

    private readonly Phase4FactEvaluator _factEvaluator;
    private readonly Phase4HallucinationDetector _hallucinationDetector;
    private readonly Phase4QuerySuggestionMapper _querySuggestionMapper;
    private readonly Phase4ScoreCalculator _scoreCalculator;
    private readonly ILogger _logger;

    public Phase4Evaluator(
        Phase4FactEvaluator? factEvaluator = null,
        Phase4HallucinationDetector? hallucinationDetector = null,
        Phase4QuerySuggestionMapper? querySuggestionMapper = null,
        Phase4ScoreCalculator? scoreCalculator = null,
        ILogger? logger = null)
    {
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<Phase4Evaluator>();
        _factEvaluator = factEvaluator ?? new Phase4FactEvaluator(_logger.ForContext<Phase4FactEvaluator>());
        _hallucinationDetector = hallucinationDetector ?? new Phase4HallucinationDetector(_logger.ForContext<Phase4HallucinationDetector>());
        _querySuggestionMapper = querySuggestionMapper ?? new Phase4QuerySuggestionMapper();
        _scoreCalculator = scoreCalculator ?? new Phase4ScoreCalculator(_logger.ForContext<Phase4ScoreCalculator>());
    }

    public EvaluationResult Evaluate(EvaluationInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var contextText = string.Join(
            "\n",
            input.SelectedChunks.Select(chunk => chunk.ChunkText ?? string.Empty));

        _logger
            .WithProperties(
                ("scenario_id", input.ScenarioId),
                ("answer_length", input.AnswerText.Length),
                ("context_length", contextText.Length))
            .Debug("Phase 4 evaluation started");

        var normalizedAnswer = Phase4TextNormalizer.Normalize(input.AnswerText);
        var normalizedContext = Phase4TextNormalizer.Normalize(contextText);

        var factResult = _factEvaluator.EvaluateNormalized(normalizedAnswer, normalizedContext);
        var hallucinationResult = _hallucinationDetector.EvaluateNormalized(normalizedAnswer);
        var querySuggestions = _querySuggestionMapper.Map(factResult.MissingLabels);
        var formatResult = Phase4FormatValidator.Validate(input.AnswerText);
        var formatPoints = formatResult.IsValid ? Phase4Constants.FormatPoints : 0;
        var contradictionDetected = Phase4PatternMatcher.ContainsAny(
            normalizedAnswer,
            NormalizedContradictionPatterns);
        var score = _scoreCalculator.Compute(
            factResult.CompletenessPoints,
            formatPoints,
            hallucinationResult.HallucinationPenalty,
            contradictionDetected);
        var presentFactLabels = factResult.PresentFactIds
            .Select(factId => Phase4RuleTables.PresentLabelByFactId.TryGetValue(factId, out var label) ? label : factId)
            .ToList();

        var scenarioResult = new PolicyRefundScenarioResult(
            presentFactLabels,
            factResult.MissingLabels,
            hallucinationResult.HallucinationFlags,
            score.Breakdown);

        _logger
            .WithProperties(
                ("scenario_id", input.ScenarioId),
                ("present_fact_count", factResult.PresentFactIds.Count),
                ("missing_fact_count", factResult.MissingLabels.Count),
                ("hallucination_flag_count", hallucinationResult.HallucinationFlags.Count),
                ("score_total", score.ScoreTotal))
            .Debug("Phase 4 evaluation completed");

        return new EvaluationResult(
            input.RunId,
            input.ScenarioId,
            score.ScoreTotal,
            querySuggestions,
            scenarioResult);
    }
}
