using System.Globalization;
using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Config;
using EvoContext.Core.Context;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Evidence;
using EvoContext.Core.Logging;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using Serilog;

namespace EvoContext.Infrastructure.Services;

public sealed class Run5Orchestrator
{
    private const int Run2ScoreThreshold = 90;
    private const int EvaluationSequenceIndex = 6;
    private const int Run2TriggeredSequenceIndex = 7;
    private const int MemoryUpdatedSequenceIndex = 7;
    private const int RunSummarySequenceIndex = 8;

    private readonly CoreConfigSnapshot _config;
    private readonly IRetriever _retriever;
    private readonly ICandidateScorer _candidateScorer;
    private readonly IRun2CandidateScorer _run2CandidateScorer;
    private readonly ICandidateRanker _candidateRanker;
    private readonly IContextSelector _selector;
    private readonly IContextPacker _packer;
    private readonly IRun2QueryBuilder _queryBuilder;
    private readonly ICandidatePoolMerger _candidatePoolMerger;
    private readonly UsefulnessMemoryStore _usefulnessMemoryStore;
    private readonly AnswerGenerationService _answerGenerationService;
    private readonly ScenarioEvaluatorDispatcher _dispatcher;
    private readonly DetectedEvidenceExtractor? _evidenceExtractor;
    private readonly ICapturingTraceEmitter _inMemoryTrace;
    private readonly ITraceEmitter _traceEmitter;
    private readonly ILogger _logger;
    private readonly IStageProgressReporter? _stageProgressReporter;

    public Run5Orchestrator(
        CoreConfigSnapshot config,
        IRetriever retriever,
        ICandidateScorer candidateScorer,
        IRun2CandidateScorer run2CandidateScorer,
        ICandidateRanker candidateRanker,
        IContextSelector selector,
        IContextPacker packer,
        IRun2QueryBuilder queryBuilder,
        ICandidatePoolMerger candidatePoolMerger,
        UsefulnessMemoryStore usefulnessMemoryStore,
        AnswerGenerationService answerGenerationService,
        ScenarioEvaluatorDispatcher dispatcher,
        ICapturingTraceEmitter inMemoryTrace,
        ITraceEmitter traceEmitter,
        ILogger? logger = null,
        IStageProgressReporter? stageProgressReporter = null,
        DetectedEvidenceExtractor? evidenceExtractor = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _candidateScorer = candidateScorer ?? throw new ArgumentNullException(nameof(candidateScorer));
        _run2CandidateScorer = run2CandidateScorer ?? throw new ArgumentNullException(nameof(run2CandidateScorer));
        _candidateRanker = candidateRanker ?? throw new ArgumentNullException(nameof(candidateRanker));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _packer = packer ?? throw new ArgumentNullException(nameof(packer));
        _queryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));
        _candidatePoolMerger = candidatePoolMerger ?? throw new ArgumentNullException(nameof(candidatePoolMerger));
        _usefulnessMemoryStore = usefulnessMemoryStore ?? throw new ArgumentNullException(nameof(usefulnessMemoryStore));
        _answerGenerationService = answerGenerationService ?? throw new ArgumentNullException(nameof(answerGenerationService));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _inMemoryTrace = inMemoryTrace ?? throw new ArgumentNullException(nameof(inMemoryTrace));
        _traceEmitter = traceEmitter ?? throw new ArgumentNullException(nameof(traceEmitter));
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<Run5Orchestrator>();
        _stageProgressReporter = stageProgressReporter;
        _evidenceExtractor = evidenceExtractor;
    }

    public async Task<Run5ExecutionResult> ExecuteAsync(
        string scenarioId,
        string queryText,
        int repeat,
        bool allowRun2 = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("Scenario ID is required.", nameof(scenarioId));
        }

        if (string.IsNullOrWhiteSpace(queryText))
        {
            throw new ArgumentException("Query text is required.", nameof(queryText));
        }

        if (repeat <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(repeat), "Repeat must be positive.");
        }

        using var compositeEmitter = new CompositeTraceEmitter(_inMemoryTrace, _traceEmitter);
        var executor = RunExecutor.ForRun3(
            _retriever,
            _candidateScorer,
            _candidateRanker,
            _selector,
            _packer,
            compositeEmitter,
            _config,
            _answerGenerationService,
            _logger.ForContext<RunExecutor>(),
            _stageProgressReporter);

        var runs = new List<Run5ExecutionRun>();
        bool? baselineRun2Triggered = null;
        string? baselineRun2Signature = null;

        var persistentSnapshot = await _usefulnessMemoryStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        UsefulnessMemorySnapshot? snapshotToPersist = null;

        for (var run = 1; run <= repeat; run++)
        {
            var events = new List<TraceEvent>();

            _inMemoryTrace.Clear();
            var run1Result = await executor
                .ExecuteAsync(
                    new RunRequest(scenarioId, queryText, RunMode.Run1AnswerGeneration),
                    cancellationToken)
                .ConfigureAwait(false);

            var run1Evaluation = ExecuteStage("Evaluating response...", () => Evaluate(run1Result));
            var run1Feedback = BuildFeedback(run1Evaluation);
            var run2Trigger = BuildRun2Trigger(run1Evaluation);

            _logger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("score_run1", run1Evaluation.ScoreTotal),
                    ("missing_item_count", run2Trigger.MissingFactLabels.Count),
                    ("should_run2", run2Trigger.ShouldRun),
                    ("reason", BuildRun2Reason(run1Evaluation, run2Trigger)))
                .Debug("Run 2 trigger evaluated");

            await compositeEmitter.EmitAsync(
                BuildEvaluationEvent(run1Result, run1Evaluation, RunMode.Run1AnswerGeneration),
                cancellationToken)
            .ConfigureAwait(false);

            events.AddRange(_inMemoryTrace.Events);

            RetrievalQuerySet? run2QuerySet = null;
            RunResult? run2Result = null;
            EvaluationResult? run2Evaluation = null;
            FeedbackOutput? run2Feedback = null;

            int memoryUpdates = 0;

            var shouldRun2 = allowRun2 && run2Trigger.ShouldRun;

            if (shouldRun2)
            {
                run2QuerySet = ExecuteStage(
                    "Triggering Run 2 — expanding retrieval queries...",
                    () => _queryBuilder.Build(queryText, run1Feedback));
                _logger
                    .WithProperties(
                        ("scenario_id", scenarioId),
                        ("expanded_query_count", run2QuerySet.AllQueries.Count),
                        ("feedback_query_count", run2QuerySet.FeedbackQueries.Count))
                    .Debug("Run 2 query set built");

                var run2TriggeredEvent = BuildRun2TriggeredEvent(run1Result, run2QuerySet);
                await _traceEmitter.EmitAsync(run2TriggeredEvent, cancellationToken).ConfigureAwait(false);
                events.Add(run2TriggeredEvent);

                _inMemoryTrace.Clear();
                run2Result = await ExecuteRun2Async(
                        scenarioId,
                        queryText,
                        run2QuerySet,
                        run1Result.RetrievalSummary.SelectedChunks,
                        run1Result.RunId,
                        persistentSnapshot,
                        compositeEmitter,
                        cancellationToken)
                    .ConfigureAwait(false);

                run2Evaluation = ExecuteStage("Evaluating response...", () => Evaluate(run2Result));
                run2Feedback = BuildFeedback(run2Evaluation);
                await compositeEmitter.EmitAsync(
                        BuildEvaluationEvent(run2Result, run2Evaluation, RunMode.Run2FeedbackExpanded),
                        cancellationToken)
                    .ConfigureAwait(false);
                events.AddRange(_inMemoryTrace.Events);
            }
            else if (baselineRun2Triggered is null)
            {
                baselineRun2Triggered = false;
            }

            if (baselineRun2Triggered is not null && baselineRun2Triggered != shouldRun2)
            {
                throw new InvalidOperationException("Determinism check failed: Run2 trigger behavior changed between repeats.");
            }

            if (run2Result is not null && run2Evaluation is not null && run2QuerySet is not null)
            {
                var signature = BuildRun2Signature(run2QuerySet, run2Result, run2Evaluation);
                if (baselineRun2Signature is null)
                {
                    baselineRun2Signature = signature;
                    baselineRun2Triggered = true;
                }
                else if (!string.Equals(signature, baselineRun2Signature, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Determinism check failed: Run2 output differs from baseline.");
                }
            }

            var scoreRun2 = run2Evaluation?.ScoreTotal ?? run1Evaluation.ScoreTotal;
            var scoreDelta = new RunScoreDelta(
                run1Evaluation.ScoreTotal,
                scoreRun2,
                scoreRun2 - run1Evaluation.ScoreTotal);

            _logger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("score_run1", scoreDelta.ScoreRun1),
                    ("score_run2", scoreDelta.ScoreRun2),
                    ("score_delta", scoreDelta.ScoreDelta))
                .Debug("Run score delta computed");

            if (scoreDelta.ScoreDelta > 0 && run2Result is not null)
            {
                var updatedSnapshot = BuildUpdatedSnapshot(persistentSnapshot, run2Result);
                snapshotToPersist ??= updatedSnapshot;
                memoryUpdates = run2Result.RetrievalSummary.SelectedChunks.Count;

                await compositeEmitter.EmitAsync(new TraceEvent(
                    TraceEventType.MemoryUpdated,
                    run2Result.RunId,
                    run2Result.RunRequestSnapshot.ScenarioId,
                    MemoryUpdatedSequenceIndex,
                    new Dictionary<string, object?>
                    {
                        ["memory_updates"] = memoryUpdates,
                        ["timestamp_utc"] = DateTimeOffset.UtcNow
                    },
                    DateTimeOffset.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            var summaryEvents = new List<TraceEvent>
            {
                BuildRunSummaryEvent(
                    run1Result,
                    run1Evaluation,
                    scoreDelta,
                    memoryUpdates: 0,
                    new[] { queryText },
                    RunMode.Run1AnswerGeneration)
            };

            if (run2Result is not null && run2Evaluation is not null && run2QuerySet is not null)
            {
                summaryEvents.Add(BuildRunSummaryEvent(
                    run2Result,
                    run2Evaluation,
                    scoreDelta,
                    memoryUpdates,
                    run2QuerySet.AllQueries,
                    RunMode.Run2FeedbackExpanded));
            }

            foreach (var summaryEvent in summaryEvents)
            {
                await _traceEmitter.EmitAsync(summaryEvent, cancellationToken).ConfigureAwait(false);
            }

            events.AddRange(summaryEvents);

            runs.Add(new Run5ExecutionRun(
                run1Result,
                run1Evaluation,
                run1Feedback,
                run2Trigger,
                run2QuerySet,
                run2Result,
                run2Evaluation,
                run2Feedback,
                scoreDelta,
                memoryUpdates,
                events));
        }

        if (snapshotToPersist is not null)
        {
            await _usefulnessMemoryStore.SaveAsync(snapshotToPersist, cancellationToken).ConfigureAwait(false);
        }

        return new Run5ExecutionResult(runs);
    }

    private static string BuildRun2Reason(EvaluationResult evaluation, Run2Trigger run2Trigger)
    {
        if (!run2Trigger.ShouldRun)
        {
            return "threshold_and_completeness_met";
        }

        return evaluation.ScoreTotal < Run2ScoreThreshold
            ? "score_below_threshold"
            : "missing_items_detected";
    }

    private EvaluationResult Evaluate(RunResult result)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        if (string.IsNullOrWhiteSpace(result.Answer))
        {
            throw new InvalidOperationException("Run result is missing an answer for evaluation.");
        }

        var selectedChunks = result.RetrievalSummary.SelectedChunks
            .Select(chunk => new SelectedChunk(
                chunk.DocumentId,
                chunk.ChunkId,
                chunk.ChunkIndex,
                chunk.ChunkText))
            .ToList();

        var input = new EvaluationInput(
            result.RunId,
            result.RunRequestSnapshot.ScenarioId,
            result.Answer,
            selectedChunks);

        return _dispatcher.Evaluate(input);
    }

    private static FeedbackOutput BuildFeedback(EvaluationResult evaluation)
    {
        if (evaluation is null)
        {
            throw new ArgumentNullException(nameof(evaluation));
        }

        if (evaluation.ScenarioResult is PolicyRefundScenarioResult policyResult)
        {
            return new FeedbackOutput(
                evaluation.RunId,
                evaluation.ScenarioId,
                evaluation.ScoreTotal,
                policyResult.ScoreBreakdown,
                policyResult.MissingFactLabels,
                policyResult.HallucinationFlags,
                evaluation.QuerySuggestions);
        }

        return new FeedbackOutput(
            evaluation.RunId,
            evaluation.ScenarioId,
            evaluation.ScoreTotal,
            new ScoreBreakdown(
                CompletenessPoints: 0,
                FormatPoints: 0,
                HallucinationPenalty: 0,
                AccuracyCapApplied: false),
            Array.Empty<string>(),
            Array.Empty<string>(),
            evaluation.QuerySuggestions);
    }

    private static Run2Trigger BuildRun2Trigger(EvaluationResult evaluation)
    {
        if (evaluation is null)
        {
            throw new ArgumentNullException(nameof(evaluation));
        }

        if (evaluation.ScenarioResult is PolicyRefundScenarioResult policyResult)
        {
            var shouldRunPolicy = evaluation.ScoreTotal < Run2ScoreThreshold
                || policyResult.MissingFactLabels.Count > 0;

            return new Run2Trigger(evaluation.ScoreTotal, policyResult.MissingFactLabels, shouldRunPolicy);
        }

        if (evaluation.ScenarioResult is Runbook502ScenarioResult runbookResult)
        {
            var shouldRunRunbook = evaluation.ScoreTotal < Run2ScoreThreshold
                || runbookResult.MissingStepLabels.Count > 0;

            return new Run2Trigger(evaluation.ScoreTotal, runbookResult.MissingStepLabels, shouldRunRunbook);
        }

        return new Run2Trigger(evaluation.ScoreTotal, Array.Empty<string>(), ShouldRun: false);
    }

    private async Task<RunResult> ExecuteRun2Async(
        string scenarioId,
        string queryText,
        RetrievalQuerySet querySet,
        IReadOnlyList<RetrievalCandidate> run1SelectedChunks,
        string run1RunId,
        UsefulnessMemorySnapshot persistentSnapshot,
        ITraceEmitter traceEmitter,
        CancellationToken cancellationToken)
    {
        if (querySet is null)
        {
            throw new ArgumentNullException(nameof(querySet));
        }

        var runId = BuildRunId(scenarioId);
        await traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.RunStarted,
            runId,
            scenarioId,
            1,
            new Dictionary<string, object?>
            {
                ["scenario_id"] = scenarioId,
                ["task_text"] = queryText,
                ["run_mode"] = RunMode.Run2FeedbackExpanded.ToString(),
                ["retrieval_queries"] = querySet.AllQueries,
                ["timestamp_utc"] = DateTimeOffset.UtcNow
            },
            DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        var allCandidates = await ExecuteStageAsync(
                "Retrieving context for query...",
                showSpinner: true,
                async innerCancellationToken =>
                {
                    var candidates = new List<RetrievalCandidate>();
                    for (var i = 0; i < querySet.AllQueries.Count; i++)
                    {
                        var query = querySet.AllQueries[i];
                        var request = new RetrievalRequest(Run2QueryIdentifier.Create(i + 1), query, _config.RetrievalN);
                        var retrievalResults = await _retriever.RetrieveAsync(request, innerCancellationToken).ConfigureAwait(false);
                        candidates.AddRange(retrievalResults);
                    }

                    return candidates;
                },
                cancellationToken)
            .ConfigureAwait(false);

        var mergedCandidates = _candidatePoolMerger.Merge(allCandidates);
        var effectiveSnapshot = BuildEffectiveSnapshot(run1SelectedChunks, run1RunId, persistentSnapshot);
        var scoredCandidates = _run2CandidateScorer.Score(mergedCandidates, effectiveSnapshot);
        var rankedCandidates = _candidateRanker.Rank(scoredCandidates);
        var rankedRetrievalCandidates = rankedCandidates
            .Select((candidate, index) => candidate.Candidate with
            {
                RankWithinQuery = index + 1,
                SimilarityScore = candidate.Score.SimilarityScore
            })
            .ToList();

        await traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.RetrievalCompleted,
            runId,
            scenarioId,
            2,
            new Dictionary<string, object?>
            {
                ["retrieval_query_count"] = querySet.AllQueries.Count,
                ["retrieved_count"] = rankedRetrievalCandidates.Count,
                ["query_text"] = queryText,
                ["retrieval_queries"] = querySet.AllQueries,
                ["candidates"] = BuildCandidateTrace(rankedRetrievalCandidates),
                ["timestamp_utc"] = DateTimeOffset.UtcNow
            },
            DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        var contextSelection = ExecuteStage(
            "Selecting top chunks...",
            () =>
            {
                var selectedChunks = _selector.Select(rankedRetrievalCandidates, _config.SelectionK);
                var contextPack = _packer.Pack(selectedChunks);
                return (SelectedChunks: selectedChunks, ContextPack: contextPack);
            });

        var selectedChunks = contextSelection.SelectedChunks;
        var contextPack = contextSelection.ContextPack;
        await traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.ContextSelected,
            runId,
            scenarioId,
            3,
            new Dictionary<string, object?>
            {
                ["selected_count"] = selectedChunks.Count,
                ["selected"] = BuildSelectedTrace(selectedChunks),
                ["context_character_count"] = contextPack.CharCount,
                ["timestamp_utc"] = DateTimeOffset.UtcNow
            },
            DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        var detectedEvidence = _evidenceExtractor?.Extract(selectedChunks);
        var generationResult = await ExecuteStageAsync(
                "Generating answer...",
                showSpinner: true,
                innerCancellationToken => _answerGenerationService.GenerateAsync(
                    queryText,
                    contextPack,
                    scenarioId,
                    detectedEvidence,
                    innerCancellationToken),
                cancellationToken)
            .ConfigureAwait(false);

        await traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.GenerationCompleted,
            runId,
            scenarioId,
            4,
            new Dictionary<string, object?>
            {
                ["prompt_question"] = queryText,
                ["prompt_context"] = contextPack.Text,
                ["prompt_template_version"] = generationResult.PromptTemplateVersion,
                ["generation_model"] = _config.GenerationModel,
                ["generation_parameters"] = new Dictionary<string, object?>
                {
                    ["temperature"] = _config.Temperature,
                    ["top_p"] = _config.TopP,
                    ["max_tokens"] = _config.MaxTokens
                },
                ["raw_model_output"] = generationResult.Answer
            },
            DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        await traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.RunFinished,
            runId,
            scenarioId,
            5,
            new Dictionary<string, object?>
            {
                ["timestamp_utc"] = DateTimeOffset.UtcNow
            },
            DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        var retrievalSummary = new RetrievalSummary(rankedRetrievalCandidates, selectedChunks, contextPack);
        return new RunResult(
            runId,
            new RunRequest(scenarioId, queryText, RunMode.Run2FeedbackExpanded),
            retrievalSummary,
            generationResult.Answer,
            null,
            detectedEvidence,
            generationResult.EvidenceBlock);
    }

    private static UsefulnessMemorySnapshot BuildEffectiveSnapshot(
        IReadOnlyList<RetrievalCandidate> run1SelectedChunks,
        string run1RunId,
        UsefulnessMemorySnapshot persistentSnapshot)
    {
        var persistentByChunkId = persistentSnapshot.Items
            .ToDictionary(item => item.ChunkId, StringComparer.Ordinal);

        var bootstrapItems = run1SelectedChunks
            .GroupBy(chunk => chunk.DocumentId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.OrderBy(chunk => chunk.ChunkIndex).First())
            .Select(chunk => new UsefulnessMemoryItem(
                chunk.ChunkId,
                chunk.DocumentId,
                UsefulnessScore: 1,
                LastUsedRunId: run1RunId))
            .ToList();

        foreach (var bootstrapItem in bootstrapItems)
        {
            if (persistentByChunkId.TryGetValue(bootstrapItem.ChunkId, out var existing))
            {
                if (bootstrapItem.UsefulnessScore > existing.UsefulnessScore)
                {
                    persistentByChunkId[bootstrapItem.ChunkId] = bootstrapItem;
                }
            }
            else
            {
                persistentByChunkId[bootstrapItem.ChunkId] = bootstrapItem;
            }
        }

        var mergedItems = persistentByChunkId.Values
            .OrderBy(item => item.DocumentId, StringComparer.Ordinal)
            .ThenBy(item => item.ChunkId, StringComparer.Ordinal)
            .ToList();

        return new UsefulnessMemorySnapshot(mergedItems);
    }

    private static UsefulnessMemorySnapshot BuildUpdatedSnapshot(
        UsefulnessMemorySnapshot persistentSnapshot,
        RunResult run2Result)
    {
        var updatedItems = persistentSnapshot.Items
            .ToDictionary(item => item.ChunkId, StringComparer.Ordinal);
        var run2RunId = run2Result.RunId;

        foreach (var chunk in run2Result.RetrievalSummary.SelectedChunks)
        {
            if (updatedItems.TryGetValue(chunk.ChunkId, out var prev))
            {
                updatedItems[chunk.ChunkId] = prev with
                {
                    UsefulnessScore = prev.UsefulnessScore + 1,
                    LastUsedRunId = run2RunId
                };
            }
            else
            {
                updatedItems[chunk.ChunkId] = new UsefulnessMemoryItem(
                    chunk.ChunkId,
                    chunk.DocumentId,
                    UsefulnessScore: 1,
                    LastUsedRunId: run2RunId);
            }
        }

        var mergedItems = updatedItems.Values
            .OrderBy(item => item.DocumentId, StringComparer.Ordinal)
            .ThenBy(item => item.ChunkId, StringComparer.Ordinal)
            .ToList();

        return new UsefulnessMemorySnapshot(mergedItems);
    }

    private static string BuildRunId(string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("Scenario ID is required.", nameof(scenarioId));
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var shortGuid = Guid.NewGuid().ToString("N")[..4];
        return $"{scenarioId}_{timestamp}_{shortGuid}";
    }

    private static IReadOnlyList<Dictionary<string, object?>> BuildCandidateTrace(IReadOnlyList<RetrievalCandidate> candidates)
    {
        var result = new List<Dictionary<string, object?>>(candidates.Count);
        foreach (var candidate in candidates)
        {
            result.Add(new Dictionary<string, object?>
            {
                ["document_id"] = candidate.DocumentId,
                ["chunk_id"] = candidate.ChunkId,
                ["chunk_index"] = candidate.ChunkIndex,
                ["similarity_score"] = candidate.SimilarityScore,
                ["chunk_char_length"] = candidate.ChunkCharLength,
                ["chunk_preview"] = BuildPreview(candidate.ChunkText, 80)
            });
        }

        return result;
    }

    private static IReadOnlyList<Dictionary<string, object?>> BuildSelectedTrace(IReadOnlyList<RetrievalCandidate> selectedChunks)
    {
        var result = new List<Dictionary<string, object?>>(selectedChunks.Count);
        foreach (var candidate in selectedChunks)
        {
            result.Add(new Dictionary<string, object?>
            {
                ["document_id"] = candidate.DocumentId,
                ["chunk_id"] = candidate.ChunkId,
                ["chunk_index"] = candidate.ChunkIndex
            });
        }

        return result;
    }

    private static TraceEvent BuildEvaluationEvent(
        RunResult result,
        EvaluationResult evaluation,
        RunMode runMode)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        if (evaluation is null)
        {
            throw new ArgumentNullException(nameof(evaluation));
        }

        var timestamp = DateTimeOffset.UtcNow;
        var metadata = new Dictionary<string, object?>
        {
            ["run_mode"] = runMode.ToString(),
            ["score_total"] = evaluation.ScoreTotal,
            ["query_suggestions"] = evaluation.QuerySuggestions,
            ["timestamp_utc"] = timestamp
        };

        switch (evaluation.ScenarioResult)
        {
            case PolicyRefundScenarioResult policyResult:
                metadata["present_fact_labels"] = policyResult.PresentFactLabels;
                metadata["missing_fact_labels"] = policyResult.MissingFactLabels;
                metadata["hallucination_flags"] = policyResult.HallucinationFlags;
                break;
            case Runbook502ScenarioResult runbookResult:
                metadata["present_step_labels"] = runbookResult.PresentStepLabels;
                metadata["missing_step_labels"] = runbookResult.MissingStepLabels;
                metadata["order_violation_labels"] = runbookResult.OrderViolationLabels;
                break;
        }

        return new TraceEvent(
            TraceEventType.EvaluationCompleted,
            result.RunId,
            result.RunRequestSnapshot.ScenarioId,
            EvaluationSequenceIndex,
            metadata,
            timestamp);
    }

    private static TraceEvent BuildRunSummaryEvent(
        RunResult result,
        EvaluationResult evaluation,
        RunScoreDelta scoreDelta,
        int memoryUpdates,
        IReadOnlyList<string> retrievalQueries,
        RunMode runMode)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new TraceEvent(
            TraceEventType.RunSummary,
            result.RunId,
            result.RunRequestSnapshot.ScenarioId,
            RunSummarySequenceIndex,
            new Dictionary<string, object?>
            {
                ["run_mode"] = runMode.ToString(),
                ["retrieval_queries"] = retrievalQueries,
                ["missing_fact_labels"] = GetPolicyMissingFactLabels(evaluation),
                ["query_suggestions"] = evaluation.QuerySuggestions,
                ["candidate_pool_size"] = result.RetrievalSummary.RetrievedCandidates.Count,
                ["selected_chunks"] = BuildSelectedTrace(result.RetrievalSummary.SelectedChunks),
                ["score_run1"] = scoreDelta.ScoreRun1,
                ["score_run2"] = scoreDelta.ScoreRun2,
                ["score_delta"] = scoreDelta.ScoreDelta,
                ["memory_updates"] = memoryUpdates,
                ["timestamp_utc"] = timestamp
            },
            timestamp);
    }

    private static TraceEvent BuildRun2TriggeredEvent(
        RunResult run1Result,
        RetrievalQuerySet querySet)
    {
        if (run1Result is null)
        {
            throw new ArgumentNullException(nameof(run1Result));
        }

        if (querySet is null)
        {
            throw new ArgumentNullException(nameof(querySet));
        }

        var timestamp = DateTimeOffset.UtcNow;
        return new TraceEvent(
            TraceEventType.Run2Triggered,
            run1Result.RunId,
            run1Result.RunRequestSnapshot.ScenarioId,
            Run2TriggeredSequenceIndex,
            new Dictionary<string, object?>
            {
                ["label"] = "Run 2 triggered — score below threshold",
                ["expanded_query_count"] = querySet.AllQueries.Count,
                ["retrieval_queries"] = querySet.AllQueries,
                ["timestamp_utc"] = timestamp
            },
            timestamp);
    }

    private static string BuildPreview(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string BuildRun2Signature(
        RetrievalQuerySet querySet,
        RunResult runResult,
        EvaluationResult evaluation)
    {
        var querySignature = string.Join("|", querySet.AllQueries);
        var selected = runResult.RetrievalSummary.SelectedChunks
            .Select(candidate => $"{candidate.DocumentId}:{candidate.ChunkId}:{candidate.ChunkIndex}")
            .ToList();
        var selectedSignature = string.Join("|", selected);
        var missingFacts = string.Join("|", GetPolicyMissingFactLabels(evaluation));
        return string.Join(
            "||",
            querySignature,
            selectedSignature,
            runResult.RetrievalSummary.ContextPack.Text,
            evaluation.ScoreTotal.ToString(CultureInfo.InvariantCulture),
            missingFacts);
    }

    private static IReadOnlyList<string> GetPolicyMissingFactLabels(EvaluationResult evaluation)
    {
        if (evaluation.ScenarioResult is PolicyRefundScenarioResult policyResult)
        {
            return policyResult.MissingFactLabels;
        }

        return Array.Empty<string>();
    }

    private T ExecuteStage<T>(string stageMessage, Func<T> action)
    {
        return _stageProgressReporter is null
            ? action()
            : _stageProgressReporter.ExecuteStage(stageMessage, action);
    }

    private Task<T> ExecuteStageAsync<T>(
        string stageMessage,
        bool showSpinner,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        return _stageProgressReporter is null
            ? action(cancellationToken)
            : _stageProgressReporter.ExecuteStageAsync(stageMessage, showSpinner, action, cancellationToken);
    }
}
