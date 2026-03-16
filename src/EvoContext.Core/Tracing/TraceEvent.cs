namespace EvoContext.Core.Tracing;

public enum TraceEventType
{
    RunStarted,
    RetrievalCompleted,
    ContextSelected,
    GenerationCompleted,
    EvaluationCompleted,
    RunFinished,
    RunSummary,
    Run2Triggered,
    MemoryUpdated,
    EmbeddingIngestCompleted,
    GateAProbeCompleted
}

public sealed record TraceEvent(
    TraceEventType EventType,
    string RunId,
    string ScenarioId,
    int SequenceIndex,
    IReadOnlyDictionary<string, object?> Metadata,
    DateTimeOffset? TimestampUtc = null);
