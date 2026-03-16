using EvoContext.Core.Runs;

namespace EvoContext.Core.Tracing;

public sealed record RunSummary(
    string RunId,
    string ScenarioId,
    RunMode RunMode,
    int ScoreRun1,
    int? ScoreRun2,
    int? ScoreDelta,
    int MemoryUpdatesCount);
