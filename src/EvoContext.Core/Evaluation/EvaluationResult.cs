namespace EvoContext.Core.Evaluation;

public sealed record EvaluationResult(
    string RunId,
    string ScenarioId,
    int ScoreTotal,
    IReadOnlyList<string> QuerySuggestions,
    IScenarioResult ScenarioResult);
