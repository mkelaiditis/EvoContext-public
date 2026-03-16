using EvoContext.Core.Evaluation;

namespace EvoContext.Core.AdaptiveMemory;

public sealed record FeedbackOutput(
    string RunId,
    string ScenarioId,
    int ScoreTotal,
    ScoreBreakdown ScoreBreakdown,
    IReadOnlyList<string> MissingFactLabels,
    IReadOnlyList<string> HallucinationFlags,
    IReadOnlyList<string> QuerySuggestions);
