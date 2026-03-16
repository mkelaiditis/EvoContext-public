using System.Collections.Generic;
using System.Linq;
using EvoContext.Core.Config;
using EvoContext.Core.Context;
using EvoContext.Core.Retrieval;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;

namespace EvoContext.Infrastructure.Services;

public sealed class Run1Orchestrator
{
    private readonly CoreConfigSnapshot _config;
    private readonly IRetriever _retriever;
    private readonly ICandidateScorer _candidateScorer;
    private readonly ICandidateRanker _candidateRanker;
    private readonly IContextSelector _selector;
    private readonly IContextPacker _packer;
    private readonly ICapturingTraceEmitter _inMemoryTrace;
    private readonly ITraceEmitter _traceEmitter;
    private readonly IStageProgressReporter? _stageProgressReporter;

    public Run1Orchestrator(
        CoreConfigSnapshot config,
        IRetriever retriever,
        ICandidateScorer candidateScorer,
        ICandidateRanker candidateRanker,
        IContextSelector selector,
        IContextPacker packer,
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
        _inMemoryTrace = inMemoryTrace ?? throw new ArgumentNullException(nameof(inMemoryTrace));
        _traceEmitter = traceEmitter ?? throw new ArgumentNullException(nameof(traceEmitter));
        _stageProgressReporter = stageProgressReporter;
    }

    public async Task<Run1ExecutionResult> ExecuteAsync(
        string scenarioId,
        string queryText,
        int repeat,
        CancellationToken cancellationToken = default)
    {
        using var compositeEmitter = new CompositeTraceEmitter(_inMemoryTrace, _traceEmitter);
        var executor = RunExecutor.ForRun1(
            _retriever,
            _candidateScorer,
            _candidateRanker,
            _selector,
            _packer,
            compositeEmitter,
            _config,
            stageProgressReporter: _stageProgressReporter);

        var runs = new List<Run1ExecutionRun>();
        string? baselineSignature = null;

        for (var run = 1; run <= repeat; run++)
        {
            _inMemoryTrace.Clear();
            var result = await executor
                .ExecuteAsync(new RunRequest(scenarioId, queryText, RunMode.Run1SimilarityOnly), cancellationToken)
                .ConfigureAwait(false);
            var signature = BuildSignature(result);

            if (run == 1)
            {
                baselineSignature = signature;
            }
            else if (!string.Equals(signature, baselineSignature, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Determinism check failed: run output differs from baseline.");
            }

            runs.Add(new Run1ExecutionRun(result, _inMemoryTrace.Events.ToList()));
        }

        return new Run1ExecutionResult(runs);
    }

    private static string BuildSignature(RunResult result)
    {
        var retrieved = result.RetrievalSummary.RetrievedCandidates
            .Select(candidate => $"{candidate.DocumentId}:{candidate.ChunkId}:{candidate.ChunkIndex}")
            .ToList();
        var selected = result.RetrievalSummary.SelectedChunks
            .Select(candidate => $"{candidate.DocumentId}:{candidate.ChunkId}:{candidate.ChunkIndex}")
            .ToList();

        return string.Join("|", retrieved) + "||" + string.Join("|", selected) + "||" + result.RetrievalSummary.ContextPack.Text;
    }
}

