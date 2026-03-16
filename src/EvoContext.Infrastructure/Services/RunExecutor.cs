using System.Globalization;
using System.Linq;
using EvoContext.Core.Config;
using EvoContext.Core.Context;
using EvoContext.Core.Logging;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using Serilog;

namespace EvoContext.Infrastructure.Services;

public sealed class RunExecutor : IRunExecutor
{
    private readonly IRetriever _retriever;
    private readonly ICandidateScorer _candidateScorer;
    private readonly ICandidateRanker _candidateRanker;
    private readonly IContextSelector _contextSelector;
    private readonly IContextPacker _contextPacker;
    private readonly ITraceEmitter _traceEmitter;
    private readonly CoreConfigSnapshot _config;
    private readonly AnswerGenerationService? _answerGenerationService;
    private readonly bool _supportsRun3;
    private readonly ILogger _logger;
    private readonly IStageProgressReporter? _stageProgressReporter;

    private RunExecutor(
        IRetriever retriever,
        ICandidateScorer candidateScorer,
        ICandidateRanker candidateRanker,
        IContextSelector contextSelector,
        IContextPacker contextPacker,
        ITraceEmitter traceEmitter,
        CoreConfigSnapshot config,
        AnswerGenerationService? answerGenerationService,
        bool supportsRun3,
        ILogger? logger,
        IStageProgressReporter? stageProgressReporter)
    {
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _candidateScorer = candidateScorer ?? throw new ArgumentNullException(nameof(candidateScorer));
        _candidateRanker = candidateRanker ?? throw new ArgumentNullException(nameof(candidateRanker));
        _contextSelector = contextSelector ?? throw new ArgumentNullException(nameof(contextSelector));
        _contextPacker = contextPacker ?? throw new ArgumentNullException(nameof(contextPacker));
        _traceEmitter = traceEmitter ?? throw new ArgumentNullException(nameof(traceEmitter));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _answerGenerationService = answerGenerationService;
        _supportsRun3 = supportsRun3;
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<RunExecutor>();
        _stageProgressReporter = stageProgressReporter;
    }

    public static RunExecutor ForRun1(
        IRetriever retriever,
        ICandidateScorer candidateScorer,
        ICandidateRanker candidateRanker,
        IContextSelector contextSelector,
        IContextPacker contextPacker,
        ITraceEmitter traceEmitter,
        CoreConfigSnapshot config,
        ILogger? logger = null,
        IStageProgressReporter? stageProgressReporter = null)
    {
        return new RunExecutor(
            retriever,
            candidateScorer,
            candidateRanker,
            contextSelector,
            contextPacker,
            traceEmitter,
            config,
            answerGenerationService: null,
            supportsRun3: false,
            logger,
            stageProgressReporter);
    }

    public static RunExecutor ForRun3(
        IRetriever retriever,
        ICandidateScorer candidateScorer,
        ICandidateRanker candidateRanker,
        IContextSelector contextSelector,
        IContextPacker contextPacker,
        ITraceEmitter traceEmitter,
        CoreConfigSnapshot config,
        AnswerGenerationService answerGenerationService,
        ILogger? logger = null,
        IStageProgressReporter? stageProgressReporter = null)
    {
        if (answerGenerationService is null)
        {
            throw new ArgumentNullException(nameof(answerGenerationService));
        }

        return new RunExecutor(
            retriever,
            candidateScorer,
            candidateRanker,
            contextSelector,
            contextPacker,
            traceEmitter,
            config,
            answerGenerationService,
            supportsRun3: true,
            logger,
            stageProgressReporter);
    }

    public async Task<RunResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.RunMode != RunMode.Run1SimilarityOnly
            && request.RunMode != RunMode.Run1AnswerGeneration
            && request.RunMode != RunMode.Run2FeedbackExpanded
            && request.RunMode != RunMode.Run3AnswerGeneration)
        {
            throw new InvalidOperationException("Unsupported run mode for RunExecutor.");
        }

        _logger
            .WithProperties(
                ("scenario_id", request.ScenarioId),
                ("run_mode", request.RunMode.ToString()),
                ("query_length", request.TaskText.Length))
            .Debug("Run executor starting");

        var runId = BuildRunId(request.ScenarioId);
        await _traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.RunStarted,
            runId,
            request.ScenarioId,
            1,
            new Dictionary<string, object?>
            {
                ["scenario_id"] = request.ScenarioId,
                ["task_text"] = request.TaskText,
                ["run_mode"] = request.RunMode.ToString(),
                ["timestamp_utc"] = DateTimeOffset.UtcNow
            },
            DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        var retrievalRequest = new RetrievalRequest(runId, request.TaskText, _config.RetrievalN);
        var retrievedCandidates = await ExecuteStageAsync(
                "Retrieving context for query...",
                showSpinner: true,
                innerCancellationToken => _retriever.RetrieveAsync(retrievalRequest, innerCancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
        var scoredCandidates = _candidateScorer.Score(retrievedCandidates);
        var rankedCandidates = _candidateRanker.Rank(scoredCandidates);
        var rankedRetrievalCandidates = rankedCandidates
            .Select((candidate, index) => candidate.Candidate with
            {
                RankWithinQuery = index + 1,
                SimilarityScore = candidate.Score.SimilarityScore
            })
            .ToList();

        _logger
            .WithProperties(
                ("scenario_id", request.ScenarioId),
                ("candidate_count", rankedRetrievalCandidates.Count),
                ("top_chunk_id", rankedRetrievalCandidates.Count > 0 ? rankedRetrievalCandidates[0].ChunkId : null))
            .Debug("Retrieval ranking completed");

        await _traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.RetrievalCompleted,
            runId,
            request.ScenarioId,
            2,
            new Dictionary<string, object?>
            {
                ["retrieval_query_count"] = 1,
                ["retrieved_count"] = rankedRetrievalCandidates.Count,
                ["query_text"] = request.TaskText,
                ["candidates"] = BuildCandidateTrace(rankedRetrievalCandidates),
                ["timestamp_utc"] = DateTimeOffset.UtcNow
            },
            DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        var contextSelection = ExecuteStage(
            "Selecting top chunks...",
            () =>
            {
                var selectedChunks = _contextSelector.Select(rankedRetrievalCandidates, _config.SelectionK);
                var contextPack = _contextPacker.Pack(selectedChunks);
                return (SelectedChunks: selectedChunks, ContextPack: contextPack);
            });

        var selectedChunks = contextSelection.SelectedChunks;
        var contextPack = contextSelection.ContextPack;

        _logger
            .WithProperties(
                ("scenario_id", request.ScenarioId),
                ("selected_chunk_count", selectedChunks.Count),
                ("context_character_count", contextPack.CharCount))
            .Debug("Context pack created");

        await _traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.ContextSelected,
            runId,
            request.ScenarioId,
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
        string? answer = null;
        AnswerGenerationResult? generationResult = null;

        if (request.RunMode == RunMode.Run3AnswerGeneration
            || request.RunMode == RunMode.Run1AnswerGeneration
            || request.RunMode == RunMode.Run2FeedbackExpanded)
        {
            if (!_supportsRun3 || _answerGenerationService is null)
            {
                throw new InvalidOperationException("RunExecutor is not configured for Run3AnswerGeneration.");
            }

            generationResult = await ExecuteStageAsync(
                    "Generating answer...",
                    showSpinner: true,
                    innerCancellationToken => _answerGenerationService.GenerateAsync(
                        request.TaskText,
                        contextPack,
                        request.ScenarioId,
                        cancellationToken: innerCancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
            answer = generationResult.Answer;

            await _traceEmitter.EmitAsync(new TraceEvent(
                TraceEventType.GenerationCompleted,
                runId,
                request.ScenarioId,
                4,
                new Dictionary<string, object?>
                {
                    ["prompt_question"] = request.TaskText,
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
        }

        var runFinishedSequence = request.RunMode == RunMode.Run3AnswerGeneration
            || request.RunMode == RunMode.Run1AnswerGeneration
            || request.RunMode == RunMode.Run2FeedbackExpanded
            ? 5
            : 4;
        await _traceEmitter.EmitAsync(new TraceEvent(
            TraceEventType.RunFinished,
            runId,
            request.ScenarioId,
            runFinishedSequence,
            new Dictionary<string, object?>
            {
                ["timestamp_utc"] = DateTimeOffset.UtcNow
            },
            DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        var retrievalSummary = new RetrievalSummary(rankedRetrievalCandidates, selectedChunks, contextPack);
        return new RunResult(runId, request, retrievalSummary, answer, null);
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

    private static string BuildPreview(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : text.Substring(0, maxLength);
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
