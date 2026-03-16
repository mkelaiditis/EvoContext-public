using EvoContext.Core.Evaluation;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;

namespace EvoContext.Core.AdaptiveMemory;

/// <summary>
/// Run2* fields are either all populated or all null for a run.
/// </summary>
public sealed record Run5ExecutionRun(
    RunResult Run1Result,
    EvaluationResult Run1Evaluation,
    FeedbackOutput Run1Feedback,
    Run2Trigger Run2Trigger,
    RetrievalQuerySet? Run2QuerySet,
    RunResult? Run2Result,
    EvaluationResult? Run2Evaluation,
    FeedbackOutput? Run2Feedback,
    RunScoreDelta ScoreDelta,
    int MemoryUpdates,
    IReadOnlyList<TraceEvent> Events);
