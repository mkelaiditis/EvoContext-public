using System.Text.Json;
using EvoContext.Core.Tracing;
using Serilog;

namespace EvoContext.Demo;

public sealed class DemoRunRenderer : IRunRenderer
{
    private const int AnswerWrapWidth = 60;

    private static readonly IReadOnlyDictionary<string, string> ItemLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MISSING_COOLING_OFF_WINDOW"] = "14-day cooling-off window",
            ["MISSING_ANNUAL_PRORATION_RULE"] = "Annual subscription proration rule",
            ["MISSING_BILLING_ERROR_EXCEPTION"] = "Billing error refund exception",
            ["MISSING_PROCESSING_TIMELINE"] = "Refund processing timeline",
            ["MISSING_CANCELLATION_PROCEDURE"] = "Cancellation procedure",
            ["PRESENT_COOLING_OFF_WINDOW"] = "14-day cooling-off window",
            ["PRESENT_ANNUAL_PRORATION_RULE"] = "Annual subscription proration rule",
            ["PRESENT_BILLING_ERROR_EXCEPTION"] = "Billing error refund exception",
            ["PRESENT_PROCESSING_TIMELINE"] = "Refund processing timeline",
            ["PRESENT_CANCELLATION_PROCEDURE"] = "Cancellation procedure",
            ["STEP_CHECK_UPSTREAM_HEALTH"] = "Check upstream service health",
            ["STEP_INSPECT_LOGS"] = "Inspect service logs",
            ["STEP_CHECK_DEPLOYMENT"] = "Inspect recent deployments",
            ["STEP_ROLLBACK_DEPLOYMENT"] = "Roll back faulty deployment"
        };

    private readonly ILogger _logger;

    private int? _run1Score;
    private int? _run2Score;
    private IReadOnlyList<string>? _run1MissingItems;
    private IReadOnlyList<string>? _run2MissingItems;
    private IReadOnlyList<string>? _run1PresentItems;
    private IReadOnlyList<string>? _run2PresentItems;
    private IReadOnlyList<string>? _run2OrderViolations;
    private string? _pendingAnswer;
    private string? _run1Answer;
    private string? _run2Answer;

    public DemoRunRenderer(ILogger logger)
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
            case TraceEventType.RetrievalCompleted:
                LogRetrieval(evt.Metadata);
                break;
            case TraceEventType.ContextSelected:
                LogContext(evt.Metadata);
                break;
            case TraceEventType.GenerationCompleted:
                LogGeneration(evt.Metadata);
                break;
            case TraceEventType.EvaluationCompleted:
                LogEvaluation(evt.Metadata);
                break;
            case TraceEventType.Run2Triggered:
                LogRun2Triggered(evt.Metadata);
                break;
            case TraceEventType.RunFinished:
                LogRunFinished(evt.Metadata);
                break;
        }
    }

    public void OnRunComplete(RunSummary summary)
    {
        if (summary is null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        _logger.Information(
            "Run summary: run_id={RunId} scenario_id={ScenarioId} mode={RunMode} score_run1={ScoreRun1} score_run2={ScoreRun2} score_delta={ScoreDelta} memory_updates={MemoryUpdates}",
            summary.RunId,
            summary.ScenarioId,
            summary.RunMode,
            summary.ScoreRun1,
            summary.ScoreRun2,
            summary.ScoreDelta,
            summary.MemoryUpdatesCount);

        if (_run1Score.HasValue)
        {
            PrintDemoSummary();
        }
    }

    private void LogRetrieval(IReadOnlyDictionary<string, object?> metadata)
    {
        var queryCount = GetInt(metadata, "retrieval_query_count") ?? 0;
        var retrieved = GetInt(metadata, "retrieved_count");
        var queryText = GetString(metadata, "query_text");

        _logger.Information(
            "Retrieval completed: query_count={QueryCount} candidates={RetrievedCount}",
            queryCount,
            retrieved ?? 0);

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            _logger.Information("Query: {QueryText}", queryText);
        }
    }

    private void LogContext(IReadOnlyDictionary<string, object?> metadata)
    {
        var selected = GetInt(metadata, "selected_count");
        var chars = GetInt(metadata, "context_character_count");
        var selectedChunkIds = JoinSelectedChunkIds(metadata, "selected");

        _logger.Information("Context selected: chunks={SelectedCount} context_chars={ContextChars}", selected ?? 0, chars ?? 0);

        if (!string.IsNullOrWhiteSpace(selectedChunkIds))
        {
            _logger.Information("Selected chunk ids: {SelectedChunkIds}", selectedChunkIds);
        }
    }

    private void LogGeneration(IReadOnlyDictionary<string, object?> metadata)
    {
        var answer = GetString(metadata, "raw_model_output") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(answer))
        {
            _pendingAnswer = null;
            _logger.Information("Generation completed.");
            return;
        }

        _pendingAnswer = answer;
        _logger.Information("Answer: {Answer}", answer);
    }

    private void LogEvaluation(IReadOnlyDictionary<string, object?> metadata)
    {
        var score = GetInt(metadata, "score_total");
        var missingItems = JoinMissingItems(metadata);
        var hallucinations = JoinList(metadata, "hallucination_flags");
        var orderViolations = JoinList(metadata, "order_violation_labels");

        _logger.Information("Evaluation completed: score_total={ScoreTotal}", score ?? 0);
        if (!string.IsNullOrWhiteSpace(missingItems))
        {
            _logger.Information("Missing items: {MissingItems}", missingItems);
        }

        if (!string.IsNullOrWhiteSpace(hallucinations))
        {
            _logger.Information("Hallucination flags: {Hallucinations}", hallucinations);
        }

        if (!string.IsNullOrWhiteSpace(orderViolations))
        {
            _logger.Information("Order violations: {OrderViolations}", orderViolations);
        }

        var runMode = GetString(metadata, "run_mode");
        var missingFacts = GetStringList(metadata, "missing_fact_labels");
        var missingSteps = GetStringList(metadata, "missing_step_labels");
        var presentFacts = GetStringList(metadata, "present_fact_labels");
        var presentSteps = GetStringList(metadata, "present_step_labels");
        var missingItemsList = (missingFacts?.Count > 0) ? missingFacts : missingSteps;
        var presentItemsList = (presentFacts?.Count > 0) ? presentFacts : presentSteps;

        if (string.Equals(runMode, "Run1AnswerGeneration", StringComparison.Ordinal))
        {
            _run1Score = score;
            _run1MissingItems = missingItemsList;
            _run1PresentItems = presentItemsList;
            _run1Answer = ClaimPendingAnswer();
        }
        else if (string.Equals(runMode, "Run2FeedbackExpanded", StringComparison.Ordinal))
        {
            _run2Score = score;
            _run2MissingItems = missingItemsList;
            _run2PresentItems = presentItemsList;
            _run2OrderViolations = GetStringList(metadata, "order_violation_labels");
            _run2Answer = ClaimPendingAnswer();
        }
    }

    private void PrintDemoSummary()
    {
        var run1Missing = _run1MissingItems ?? Array.Empty<string>();
        var run2Missing = _run2MissingItems ?? Array.Empty<string>();
        var recovered = run1Missing.Except(run2Missing, StringComparer.Ordinal).ToList();
        var stillMissing = run2Missing.ToList();
        var orderViolations = _run2OrderViolations ?? Array.Empty<string>();
        var delta = (_run2Score ?? 0) - (_run1Score ?? 0);
        var deltaSign = delta >= 0 ? "+" : string.Empty;
        var hasRun2Result = _run2Score.HasValue;

        _logger.Information(string.Empty);
        _logger.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger.Information("  RUN 1 RESULT");
        _logger.Information("  Score: {Score}", _run1Score);

        if (run1Missing.Count > 0)
        {
            _logger.Information("  Missing items:");
            foreach (var label in run1Missing)
            {
                _logger.Information("    * {Label}", ToHumanLabel(label));
            }
        }
        else
        {
            _logger.Information("  Missing items: (none)");
        }

        LogAnswerSection("Run 1 answer:", _run1Answer, _run1PresentItems, run1Missing);

        if (hasRun2Result)
        {
            _logger.Information(string.Empty);
            _logger.Information("  RUN 2 RESULT");
            _logger.Information("  Score: {Score}", _run2Score);

            if (recovered.Count > 0)
            {
                _logger.Information("  Recovered items:");
                foreach (var label in recovered)
                {
                    _logger.Information("    + {Label}", ToHumanLabel(label));
                }
            }

            if (stillMissing.Count > 0)
            {
                _logger.Information("  Still missing:");
                foreach (var label in stillMissing)
                {
                    _logger.Information("    * {Label}", ToHumanLabel(label));
                }
            }
            else
            {
                _logger.Information("  Still missing: (none)");
            }

            if (orderViolations.Count > 0)
            {
                _logger.Information("  Order violations:");
                foreach (var label in orderViolations)
                {
                    _logger.Information("    ! {Label}", ToHumanLabel(label));
                }
            }

            LogAnswerSection("Run 2 answer:", _run2Answer, _run2PresentItems, stillMissing);

            _logger.Information(string.Empty);
            _logger.Information(string.Concat("  Score improvement: ", deltaSign, delta));
        }

        _logger.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger.Information(string.Empty);
    }

    private void LogAnswerSection(
        string label,
        string? answer,
        IReadOnlyList<string>? presentItems = null,
        IReadOnlyList<string>? missingItems = null)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return;
        }

        _logger.Information(string.Empty);
        _logger.Information(string.Concat("  ", label));

        foreach (var line in WrapAnswer(answer, AnswerWrapWidth))
        {
            _logger.Information(string.Concat("    ", line));
        }

        LogAnswerBadges(presentItems, missingItems);
    }

    private void LogAnswerBadges(IReadOnlyList<string>? presentItems, IReadOnlyList<string>? missingItems)
    {
        if (presentItems is null)
        {
            return;
        }

        var presentText = presentItems.Count == 0
            ? "(none)"
            : string.Join(", ", presentItems.Select(ToHumanLabel));
        var missingText = missingItems is null || missingItems.Count == 0
            ? "(none)"
            : string.Join(", ", missingItems.Select(ToHumanLabel));

        _logger.Information(string.Concat("    Present:  ", presentText));
        _logger.Information(string.Concat("    Missing:  ", missingText));
    }

    private string? ClaimPendingAnswer()
    {
        if (string.IsNullOrWhiteSpace(_pendingAnswer))
        {
            _pendingAnswer = null;
            return null;
        }

        var answer = _pendingAnswer;
        _pendingAnswer = null;
        return answer;
    }

    private static string ToHumanLabel(string label)
    {
        return ItemLabels.TryGetValue(label, out var human) ? human : label;
    }

    private void LogRun2Triggered(IReadOnlyDictionary<string, object?> metadata)
    {
        var label = GetString(metadata, "label") ?? "Run 2 triggered - score below threshold";
        var expandedQueryCount = GetInt(metadata, "expanded_query_count") ?? 0;
        _logger.Information("{Label}: expanded_queries={ExpandedQueryCount}", label, expandedQueryCount);
    }

    private void LogRunFinished(IReadOnlyDictionary<string, object?> metadata)
    {
        var runId = GetString(metadata, "run_id");
        var scenarioId = GetString(metadata, "scenario_id");
        var runMode = GetString(metadata, "run_mode");
        var queryText = GetString(metadata, "query_text");
        var scoreRun1 = GetInt(metadata, "score_run1");
        var scoreRun2 = GetInt(metadata, "score_run2");
        var scoreDelta = GetInt(metadata, "score_delta");
        var memoryUpdatesCount = GetInt(metadata, "memory_updates_count") ?? 0;

        if (string.IsNullOrWhiteSpace(runId)
            && string.IsNullOrWhiteSpace(scenarioId)
            && string.IsNullOrWhiteSpace(runMode)
            && string.IsNullOrWhiteSpace(queryText)
            && scoreRun1 is null
            && scoreRun2 is null
            && scoreDelta is null)
        {
            _logger.Information("Run finished.");
            return;
        }

        _logger.Information(
            "Run finished: run_id={RunId} scenario_id={ScenarioId} mode={RunMode} query={QueryText} score_run1={ScoreRun1} score_run2={ScoreRun2} score_delta={ScoreDelta} memory_updates={MemoryUpdatesCount}",
            runId,
            scenarioId,
            runMode,
            queryText,
            scoreRun1,
            scoreRun2,
            scoreDelta,
            memoryUpdatesCount);
    }

    private static int? GetInt(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsed)
                => parsed,
            _ => null
        };
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string stringValue => stringValue,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            _ => null
        };
    }

    private static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            IReadOnlyList<string> list => list,
            IEnumerable<string> values => values.ToList(),
            JsonElement element when element.ValueKind == JsonValueKind.Array
                => element.EnumerateArray().Select(e => e.ToString()).ToList(),
            _ => null
        };
    }

    private static string JoinMissingItems(IReadOnlyDictionary<string, object?> metadata)
    {
        return JoinList(metadata, "missing_items");
    }

    private static string JoinList(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        return value switch
        {
            IReadOnlyList<string> list => string.Join(", ", list),
            IEnumerable<string> values => string.Join(", ", values),
            JsonElement element when element.ValueKind == JsonValueKind.Array
                => string.Join(", ", element.EnumerateArray().Select(e => e.ToString())),
            _ => string.Empty
        };
    }

    private static string JoinSelectedChunkIds(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            var ids = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var documentId = item.TryGetProperty("document_id", out var documentIdElement)
                    ? documentIdElement.ToString()
                    : string.Empty;
                var chunkId = item.TryGetProperty("chunk_id", out var chunkIdElement)
                    ? chunkIdElement.ToString()
                    : string.Empty;
                var chunkIndex = item.TryGetProperty("chunk_index", out var chunkIndexElement)
                    ? chunkIndexElement.ToString()
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(documentId)
                    || !string.IsNullOrWhiteSpace(chunkId)
                    || !string.IsNullOrWhiteSpace(chunkIndex))
                {
                    ids.Add(string.Concat(documentId, ":", chunkId, ":", chunkIndex));
                }
            }

            return string.Join(", ", ids);
        }

        if (value is IEnumerable<Dictionary<string, object?>> dictionaries)
        {
            var ids = dictionaries
                .Select(item =>
                {
                    item.TryGetValue("document_id", out var documentId);
                    item.TryGetValue("chunk_id", out var chunkId);
                    item.TryGetValue("chunk_index", out var chunkIndex);
                    return string.Concat(documentId, ":", chunkId, ":", chunkIndex);
                })
                .ToList();

            return string.Join(", ", ids);
        }

        return string.Empty;
    }

    private static IEnumerable<string> WrapAnswer(string answer, int width)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            yield break;
        }

        foreach (var sourceLine in answer.Split('\n'))
        {
            var remaining = sourceLine.TrimEnd('\r');

            if (remaining.Length == 0)
            {
                yield return string.Empty;
                continue;
            }

            while (remaining.Length > width)
            {
                var splitAt = remaining.LastIndexOf(' ', width);
                if (splitAt <= 0)
                {
                    splitAt = width;
                }

                yield return remaining[..splitAt].TrimEnd();
                remaining = remaining[splitAt..].TrimStart();
            }

            if (remaining.Length > 0)
            {
                yield return remaining;
            }
        }
    }
}
