namespace EvoContext.Core.Evaluation;

public sealed record EvaluationInput(
    string RunId,
    string ScenarioId,
    string AnswerText,
    IReadOnlyList<SelectedChunk> SelectedChunks);
