namespace EvoContext.Core.AdaptiveMemory;

public sealed record Run2Trigger(
    int ScoreTotal,
    IReadOnlyList<string> MissingFactLabels,
    bool ShouldRun);
