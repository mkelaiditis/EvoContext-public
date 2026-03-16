namespace EvoContext.Core.Evaluation;

public interface IScenarioEvaluator
{
    string ScenarioId { get; }

    EvaluationResult Evaluate(EvaluationInput input);
}
