using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Core.Evaluation;

public sealed class ScenarioEvaluatorDispatcher
{
    private readonly IReadOnlyDictionary<string, IScenarioEvaluator> _evaluators;
    private readonly ILogger _logger;

    public ScenarioEvaluatorDispatcher(IEnumerable<IScenarioEvaluator> evaluators, ILogger? logger = null)
    {
        if (evaluators is null)
        {
            throw new ArgumentNullException(nameof(evaluators));
        }

        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<ScenarioEvaluatorDispatcher>();

        var dictionary = new Dictionary<string, IScenarioEvaluator>(StringComparer.Ordinal);
        foreach (var evaluator in evaluators)
        {
            if (evaluator is null)
            {
                throw new ArgumentException("Evaluator instance cannot be null.", nameof(evaluators));
            }

            if (string.IsNullOrWhiteSpace(evaluator.ScenarioId))
            {
                throw new ArgumentException("Scenario evaluator must declare a scenario id.", nameof(evaluators));
            }

            if (!dictionary.TryAdd(evaluator.ScenarioId, evaluator))
            {
                throw new InvalidOperationException(
                    $"Duplicate evaluator registration for scenario_id {evaluator.ScenarioId}.");
            }
        }

        _evaluators = dictionary;
    }

    public EvaluationResult Evaluate(EvaluationInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (!_evaluators.TryGetValue(input.ScenarioId, out var evaluator))
        {
            throw new InvalidOperationException(
                $"No evaluator registered for scenario_id {input.ScenarioId}.");
        }

        _logger
            .WithProperties(
                ("scenario_id", input.ScenarioId),
                ("evaluator_type", evaluator.GetType().Name))
            .Debug("Evaluator selected");

        return evaluator.Evaluate(input);
    }
}
