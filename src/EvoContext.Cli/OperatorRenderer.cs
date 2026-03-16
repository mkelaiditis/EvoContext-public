using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using Serilog;

namespace EvoContext.Cli;

public sealed class OperatorRenderer : IRunRenderer
{
    private readonly ILogger _logger;

    public OperatorRenderer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void OnEvent(TraceEvent evt)
    {
        if (evt is null)
        {
            throw new ArgumentNullException(nameof(evt));
        }

        switch (evt.EventType)
        {
            case TraceEventType.RunStarted:
                _logger.Information(
                    "event=RunStarted run_id={RunId} scenario_id={ScenarioId} sequence={SequenceIndex} run_mode={RunMode}",
                    evt.RunId,
                    evt.ScenarioId,
                    evt.SequenceIndex,
                    GetString(evt.Metadata, "run_mode"));
                break;
            case TraceEventType.RetrievalCompleted:
                _logger.Information(
                    "event=RetrievalCompleted run_id={RunId} scenario_id={ScenarioId} sequence={SequenceIndex} retrieval_query_count={RetrievalQueryCount} retrieved_count={RetrievedCount}",
                    evt.RunId,
                    evt.ScenarioId,
                    evt.SequenceIndex,
                    GetValue(evt.Metadata, "retrieval_query_count"),
                    GetValue(evt.Metadata, "retrieved_count"));
                break;
            case TraceEventType.ContextSelected:
                _logger.Information(
                    "event=ContextSelected run_id={RunId} scenario_id={ScenarioId} sequence={SequenceIndex} selected_count={SelectedCount} context_character_count={ContextChars}",
                    evt.RunId,
                    evt.ScenarioId,
                    evt.SequenceIndex,
                    GetValue(evt.Metadata, "selected_count"),
                    GetValue(evt.Metadata, "context_character_count"));
                break;
            case TraceEventType.GenerationCompleted:
                _logger.Information(
                    "event=GenerationCompleted run_id={RunId} scenario_id={ScenarioId} sequence={SequenceIndex} answer_length={AnswerLength}",
                    evt.RunId,
                    evt.ScenarioId,
                    evt.SequenceIndex,
                    GetString(evt.Metadata, "raw_model_output").Length);
                break;
            case TraceEventType.EvaluationCompleted:
                _logger.Information(
                    "event=EvaluationCompleted run_id={RunId} scenario_id={ScenarioId} sequence={SequenceIndex} run_mode={RunMode} score_total={ScoreTotal} missing_items={MissingItems} missing_fact_labels={MissingFactLabels} missing_step_labels={MissingStepLabels} order_violation_labels={OrderViolationLabels}",
                    evt.RunId,
                    evt.ScenarioId,
                    evt.SequenceIndex,
                    GetString(evt.Metadata, "run_mode"),
                    GetValue(evt.Metadata, "score_total"),
                    JoinList(evt.Metadata, "missing_items"),
                    JoinList(evt.Metadata, "missing_fact_labels"),
                    JoinList(evt.Metadata, "missing_step_labels"),
                    JoinList(evt.Metadata, "order_violation_labels"));
                _logger.Information("");
                break;
            case TraceEventType.Run2Triggered:
                _logger.Information(
                    "event=Run2Triggered run_id={RunId} scenario_id={ScenarioId} sequence={SequenceIndex} label={Label} expanded_query_count={ExpandedQueryCount}",
                    evt.RunId,
                    evt.ScenarioId,
                    evt.SequenceIndex,
                    GetString(evt.Metadata, "label"),
                    GetValue(evt.Metadata, "expanded_query_count"));
                _logger.Information("");
                break;
            case TraceEventType.RunFinished:
                _logger.Information(
                    "event=RunFinished run_id={RunId} scenario_id={ScenarioId} sequence={SequenceIndex}",
                    evt.RunId,
                    evt.ScenarioId,
                    evt.SequenceIndex);
                break;
            case TraceEventType.RunSummary:
                _logger.Information(
                    "event=RunSummaryEvent run_id={RunId} scenario_id={ScenarioId} sequence={SequenceIndex} run_mode={RunMode} score_run1={ScoreRun1} score_run2={ScoreRun2} score_delta={ScoreDelta} memory_updates={MemoryUpdates}",
                    evt.RunId,
                    evt.ScenarioId,
                    evt.SequenceIndex,
                    GetString(evt.Metadata, "run_mode"),
                    GetValue(evt.Metadata, "score_run1"),
                    GetValue(evt.Metadata, "score_run2"),
                    GetValue(evt.Metadata, "score_delta"),
                    GetValue(evt.Metadata, "memory_updates"));
                break;
            default:
                _logger.Information(
                    "event={EventType} run_id={RunId} scenario_id={ScenarioId} sequence={SequenceIndex}",
                    evt.EventType,
                    evt.RunId,
                    evt.ScenarioId,
                    evt.SequenceIndex);
                break;
        }
    }

    public void OnRunComplete(RunSummary summary)
    {
        if (summary is null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        _logger.Information("");
        _logger.Information(
            "event=RunSummary run_id={RunId} scenario_id={ScenarioId} run_mode={RunMode} score_run1={ScoreRun1} score_run2={ScoreRun2} score_delta={ScoreDelta} memory_updates={MemoryUpdates}",
            summary.RunId,
            summary.ScenarioId,
            summary.RunMode,
            summary.ScoreRun1,
            summary.ScoreRun2,
            summary.ScoreDelta,
            summary.MemoryUpdatesCount);
    }

    private static object? GetValue(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) ? value : null;
    }

    private static string GetString(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value)
            ? Convert.ToString(value) ?? string.Empty
            : string.Empty;
    }

    private static string JoinList(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        if (value is IReadOnlyList<string> list)
        {
            return string.Join(',', list);
        }

        if (value is IEnumerable<string> enumerable)
        {
            return string.Join(',', enumerable);
        }

        return Convert.ToString(value) ?? string.Empty;
    }
}
