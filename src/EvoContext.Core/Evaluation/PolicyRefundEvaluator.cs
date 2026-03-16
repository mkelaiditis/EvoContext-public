using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Core.Evaluation;

public sealed class PolicyRefundEvaluator : IScenarioEvaluator
{
    public const string PolicyScenarioId = "policy_refund_v1";

    private readonly Phase4Evaluator _evaluator;

    public PolicyRefundEvaluator(Phase4Evaluator? evaluator = null, ILogger? logger = null)
    {
        _evaluator = evaluator ?? new Phase4Evaluator(logger: logger ?? StructuredLogging.NullLogger);
    }

    public string ScenarioId => PolicyScenarioId;

    public EvaluationResult Evaluate(EvaluationInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (!string.Equals(input.ScenarioId, ScenarioId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Only scenario_id {ScenarioId} is supported by {nameof(PolicyRefundEvaluator)}.");
        }

        return _evaluator.Evaluate(input);
    }
}
