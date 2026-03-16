using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Core.Evaluation;

public sealed class Runbook502Evaluator : IScenarioEvaluator
{
    public const string RunbookScenarioId = "runbook_502_v1";

    private readonly Runbook502StepEvaluator _stepEvaluator;
    private readonly Runbook502HallucinationDetector _hallucinationDetector;
    private readonly Runbook502QuerySuggestionMapper _querySuggestionMapper;
    private readonly Runbook502ScoreCalculator _scoreCalculator;
    private readonly ILogger _logger;

    public string ScenarioId => RunbookScenarioId;

    public Runbook502Evaluator(
        Runbook502StepEvaluator? stepEvaluator = null,
        Runbook502HallucinationDetector? hallucinationDetector = null,
        Runbook502QuerySuggestionMapper? querySuggestionMapper = null,
        Runbook502ScoreCalculator? scoreCalculator = null,
        ILogger? logger = null)
    {
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<Runbook502Evaluator>();
        _stepEvaluator = stepEvaluator ?? new Runbook502StepEvaluator(_logger.ForContext<Runbook502StepEvaluator>());
        _hallucinationDetector = hallucinationDetector ?? new Runbook502HallucinationDetector(_logger.ForContext<Runbook502HallucinationDetector>());
        _querySuggestionMapper = querySuggestionMapper ?? new Runbook502QuerySuggestionMapper();
        _scoreCalculator = scoreCalculator ?? new Runbook502ScoreCalculator(_logger.ForContext<Runbook502ScoreCalculator>());
    }

    public EvaluationResult Evaluate(EvaluationInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (!string.Equals(input.ScenarioId, ScenarioId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Only scenario_id {ScenarioId} is supported by {nameof(Runbook502Evaluator)}.");
        }

        var contextText = string.Join(
            "\n",
            input.SelectedChunks.Select(chunk => chunk.ChunkText ?? string.Empty));

        _logger
            .WithProperties(
                ("scenario_id", input.ScenarioId),
                ("answer_length", input.AnswerText.Length),
                ("context_length", contextText.Length))
            .Debug("Runbook 502 evaluation started");

        var normalizedAnswer = Phase4TextNormalizer.Normalize(input.AnswerText);
        var normalizedContext = Phase4TextNormalizer.Normalize(contextText);

        var stepResult = _stepEvaluator.EvaluateNormalized(normalizedAnswer, normalizedContext);
        var hallucinationResult = _hallucinationDetector.EvaluateNormalized(normalizedAnswer, normalizedContext);
        var querySuggestions = _querySuggestionMapper.Map(stepResult.MissingStepLabels);

        var score = _scoreCalculator.Compute(
            stepResult.PresentStepLabels.Count,
            stepResult.PresentStepLabels.Count,
            stepResult.OrderViolationLabels.Count > 0,
            HasListFormat(input.AnswerText),
            hallucinationResult.HallucinationPenalty);

        var scenarioResult = new Runbook502ScenarioResult(
            stepResult.PresentStepLabels,
            stepResult.MissingStepLabels,
            stepResult.OrderViolationLabels,
            score.Breakdown);

        _logger
            .WithProperties(
                ("scenario_id", input.ScenarioId),
                ("present_step_count", stepResult.PresentStepLabels.Count),
                ("missing_step_count", stepResult.MissingStepLabels.Count),
                ("order_violation_count", stepResult.OrderViolationLabels.Count),
                ("score_total", score.ScoreTotal))
            .Debug("Runbook 502 evaluation completed");

        return new EvaluationResult(
            input.RunId,
            input.ScenarioId,
            score.ScoreTotal,
            querySuggestions,
            scenarioResult);
    }

    private static bool HasListFormat(string answerText)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            answerText,
            "(?m)^\\s*(?:[-*]|\\d+[\\.)])\\s+\\S+",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}
