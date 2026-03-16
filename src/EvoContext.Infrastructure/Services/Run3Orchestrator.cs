using EvoContext.Core.Config;
using EvoContext.Core.Context;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;

namespace EvoContext.Infrastructure.Services;

public sealed class Run3Orchestrator
{
    private readonly CoreConfigSnapshot _config;
    private readonly IRetriever _retriever;
    private readonly ICandidateScorer _candidateScorer;
    private readonly ICandidateRanker _candidateRanker;
    private readonly IContextSelector _selector;
    private readonly IContextPacker _packer;
    private readonly AnswerGenerationService _answerGenerationService;
    private readonly ICapturingTraceEmitter _inMemoryTrace;
    private readonly ITraceEmitter _traceEmitter;
    private readonly IStageProgressReporter? _stageProgressReporter;

    public Run3Orchestrator(
        CoreConfigSnapshot config,
        IRetriever retriever,
        ICandidateScorer candidateScorer,
        ICandidateRanker candidateRanker,
        IContextSelector selector,
        IContextPacker packer,
        AnswerGenerationService answerGenerationService,
        ICapturingTraceEmitter inMemoryTrace,
        ITraceEmitter traceEmitter,
        IStageProgressReporter? stageProgressReporter = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _candidateScorer = candidateScorer ?? throw new ArgumentNullException(nameof(candidateScorer));
        _candidateRanker = candidateRanker ?? throw new ArgumentNullException(nameof(candidateRanker));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _packer = packer ?? throw new ArgumentNullException(nameof(packer));
        _answerGenerationService = answerGenerationService ?? throw new ArgumentNullException(nameof(answerGenerationService));
        _inMemoryTrace = inMemoryTrace ?? throw new ArgumentNullException(nameof(inMemoryTrace));
        _traceEmitter = traceEmitter ?? throw new ArgumentNullException(nameof(traceEmitter));
        _stageProgressReporter = stageProgressReporter;
    }

    public async Task<Run3ExecutionResult> ExecuteAsync(
        string scenarioId,
        string queryText,
        int repeat,
        CancellationToken cancellationToken = default)
    {
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
            stageProgressReporter: _stageProgressReporter);

        var runs = new List<Run3ExecutionRun>();
        string? baselineAnswer = null;

        for (var run = 1; run <= repeat; run++)
        {
            _inMemoryTrace.Clear();
            var result = await executor
                .ExecuteAsync(new RunRequest(scenarioId, queryText, RunMode.Run3AnswerGeneration), cancellationToken)
                .ConfigureAwait(false);
            var answer = result.Answer ?? string.Empty;

            if (run == 1)
            {
                baselineAnswer = answer;
            }
            else if (!string.Equals(answer, baselineAnswer, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Determinism check failed: run output differs from baseline.");
            }

            runs.Add(new Run3ExecutionRun(result, _inMemoryTrace.Events.ToList()));
        }

        return new Run3ExecutionResult(runs);
    }
}
