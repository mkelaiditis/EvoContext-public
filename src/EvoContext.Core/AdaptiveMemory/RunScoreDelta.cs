namespace EvoContext.Core.AdaptiveMemory;

public sealed record RunScoreDelta(
    int ScoreRun1,
    int ScoreRun2,
    int ScoreDelta);
