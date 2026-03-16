using EvoContext.Cli.Utilities;
using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Config;
using EvoContext.Core.Documents;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Logging;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Configuration;
using EvoContext.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace EvoContext.Cli.Services;

public sealed class CliCommandExecutor
{
    private const int ExitOk = 0;
    private const int ExitUsage = 2;
    private static readonly IRetrievalSummaryRenderer RetrievalSummaryRenderer = new RetrievalSummaryRenderer();

    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly Func<ILogger> _screenLoggerFactory;
    private readonly Func<ILogger, IRunRenderer>? _rendererFactory;
    private readonly Func<ILogger, IStageProgressReporter>? _stageProgressReporterFactory;
    private readonly Func<
        ILogger,
        IConfiguration,
        string,
        string,
        int,
        bool,
        ITraceEmitter?,
        IStageProgressReporter?,
        Task<Run5ExecutionResult>> _run5Executor;

    public CliCommandExecutor(ILogger logger, IConfiguration configuration, Func<ILogger> screenLoggerFactory)
        : this(logger, configuration, screenLoggerFactory, rendererFactory: null, stageProgressReporterFactory: null)
    {
    }

    public CliCommandExecutor(
        ILogger logger,
        IConfiguration configuration,
        Func<ILogger> screenLoggerFactory,
        Func<ILogger, IRunRenderer>? rendererFactory = null)
        : this(logger, configuration, screenLoggerFactory, rendererFactory, stageProgressReporterFactory: null)
    {
    }

    public CliCommandExecutor(
        ILogger logger,
        IConfiguration configuration,
        Func<ILogger> screenLoggerFactory,
        Func<ILogger, IRunRenderer>? rendererFactory,
        Func<ILogger, IStageProgressReporter>? stageProgressReporterFactory)
        : this(
            logger,
            configuration,
            screenLoggerFactory,
            rendererFactory,
            stageProgressReporterFactory,
            run5Executor: null)
    {
    }

    public CliCommandExecutor(
        ILogger logger,
        IConfiguration configuration,
        Func<ILogger> screenLoggerFactory,
        Func<ILogger, IRunRenderer>? rendererFactory,
        Func<ILogger, IStageProgressReporter>? stageProgressReporterFactory,
        Func<
            ILogger,
            IConfiguration,
            string,
            string,
            int,
            bool,
            ITraceEmitter?,
            IStageProgressReporter?,
            Task<Run5ExecutionResult>>? run5Executor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _screenLoggerFactory = screenLoggerFactory ?? throw new ArgumentNullException(nameof(screenLoggerFactory));
        _rendererFactory = rendererFactory;
        _stageProgressReporterFactory = stageProgressReporterFactory;
        _run5Executor = run5Executor ?? Run5ServiceFactory.ExecuteRun5Async;
    }

    public async Task<int> IngestAsync(string datasetPath)
    {
        try
        {
            _logger
                .WithProperties(("dataset_path", datasetPath))
                .Debug("Ingest command execution starting");

            var ingestionService = new DocumentIngestionService();
            var config = new CoreConfigLoader(_configuration).Load();
            var result = await ingestionService
                .IngestAsync(datasetPath, config.ChunkSizeChars, config.ChunkOverlapChars)
                .ConfigureAwait(false);

            var summary = IngestionSummaryFormatter.Format(
                result.Documents,
                result.DocumentsSkipped,
                result.Chunks);

            _logger.Information(summary.TrimEnd('\r', '\n'));
            IngestionLogger.WriteDocumentDetails(_logger, result.Documents, result.Chunks);

            return ExitOk;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ingest failed: {Message}", ex.Message);
            return ExitUsage;
        }
    }

    public async Task<int> EmbedAsync(string scenarioId, string datasetPath)
    {
        try
        {
            _logger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("dataset_path", datasetPath))
                .Debug("Embed command execution starting");

            var collectionName = CliPathResolver.BuildScenarioCollectionName(scenarioId);
            var gateConfig = GateAConfig.Load(
                _configuration["QDRANT_URL"],
                _configuration["QDRANT_API_KEY"],
                collectionName);
            var config = new CoreConfigLoader(_configuration).Load();
            var loader = new MarkdownDocumentLoader();
            var chunker = new Chunker(config.ChunkSizeChars, config.ChunkOverlapChars);
            var embedder = new EmbeddingService(config, _configuration["OPENAI_API_KEY"], _logger);
            var indexService = new QdrantIndexService(
                gateConfig.Host,
                gateConfig.Port,
                gateConfig.UseHttps,
                gateConfig.ApiKey,
                gateConfig.CollectionName);
            var retriever = new RetrievalService(
                gateConfig.Host,
                gateConfig.Port,
                gateConfig.UseHttps,
                gateConfig.ApiKey,
                gateConfig.CollectionName,
                config,
                embedder,
                _logger);
            var probeWriter = new GateAProbeWriter();
            var stageProgressReporter = CreateStageProgressReporter();
            using var traceEmitter = new TraceEmitter();

            var pipeline = new EmbeddingPipelineService(
                config,
                gateConfig.CollectionName,
                loader,
                chunker,
                embedder,
                indexService,
                retriever,
                probeWriter,
                traceEmitter,
                _logger,
                stageProgressReporter);

            var result = await pipeline.ExecuteAsync(scenarioId, datasetPath).ConfigureAwait(false);
            _logger.Information(
                "embedding_ingest_completed documents={Documents} chunks={Chunks} vector_dimension={VectorDimension}",
                result.DocumentCount,
                result.ChunkCount,
                result.VectorDimension);
            _logger.Information(
                "gate_a_probe_completed doc6_in_top3={Doc6InTop3}",
                result.Doc6InTop3);

            return result.Doc6InTop3 ? ExitUsage : ExitOk;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "embed failed: {Message}", ex.Message);
            return ExitUsage;
        }
    }

    public async Task<int> Run1Async(
        string scenarioId,
        string queryText,
        int repeat)
    {
        try
        {
            _logger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("repeat", repeat),
                    ("query_length", queryText.Length))
                .Debug("Run1 command execution starting");

            var collectionName = CliPathResolver.BuildScenarioCollectionName(scenarioId);
            var gateConfig = GateAConfig.Load(
                _configuration["QDRANT_URL"],
                _configuration["QDRANT_API_KEY"],
                collectionName);
            var config = new CoreConfigLoader(_configuration).Load();
            var embedder = new EmbeddingService(config, _configuration["OPENAI_API_KEY"], _logger);
            var retriever = new RetrievalService(
                gateConfig.Host,
                gateConfig.Port,
                gateConfig.UseHttps,
                gateConfig.ApiKey,
                gateConfig.CollectionName,
                config,
                embedder,
                _logger);
            var scorer = new CandidateScorer();
            var ranker = new CandidateRanker();
            var selector = new ContextSelector();
            var packer = new ContextPackPacker(config.ContextBudgetChars);
            var inMemoryTrace = new InMemoryTraceEmitter();
            var stageProgressReporter = CreateStageProgressReporter();
            using var traceEmitter = new TraceEmitter();

            var orchestrator = new Run1Orchestrator(
                config,
                retriever,
                scorer,
                ranker,
                selector,
                packer,
                inMemoryTrace,
                traceEmitter,
                stageProgressReporter);
            var execution = await orchestrator
                .ExecuteAsync(scenarioId, queryText, repeat)
                .ConfigureAwait(false);

            for (var i = 0; i < execution.Runs.Count; i++)
            {
                var run = execution.Runs[i];
                WriteRunSummary(_logger, run.Result, i + 1, repeat);
            }

            return ExitOk;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "run1 failed: {Message}", ex.Message);
            return ExitUsage;
        }
    }

    public async Task<int> Run3Async(
        string scenarioId,
        string queryText,
        int repeat)
    {
        try
        {
            _logger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("repeat", repeat),
                    ("query_length", queryText.Length))
                .Debug("Run3 command execution starting");

            var collectionName = CliPathResolver.BuildScenarioCollectionName(scenarioId);
            var gateConfig = GateAConfig.Load(
                _configuration["QDRANT_URL"],
                _configuration["QDRANT_API_KEY"],
                collectionName);
            var config = new CoreConfigLoader(_configuration).Load();
            var embedder = new EmbeddingService(config, _configuration["OPENAI_API_KEY"], _logger);
            var retriever = new RetrievalService(
                gateConfig.Host,
                gateConfig.Port,
                gateConfig.UseHttps,
                gateConfig.ApiKey,
                gateConfig.CollectionName,
                config,
                embedder,
                _logger);
            var scorer = new CandidateScorer();
            var ranker = new CandidateRanker();
            var selector = new ContextSelector();
            var packer = new ContextPackPacker(config.ContextBudgetChars);
            var generator = new GenerationService(config, _configuration["OPENAI_API_KEY"], _logger);
            var promptBuilder = new Phase3PromptBuilder();
            var validator = new AnswerFormatValidator();
            var answerService = new AnswerGenerationService(promptBuilder, generator, validator, _logger);
            var inMemoryTrace = new InMemoryTraceEmitter();
            var screenLogger = _screenLoggerFactory();
            var stageProgressReporter = CreateStageProgressReporter(screenLogger);
            using var traceEmitter = new TraceEmitter();

            var orchestrator = new Run3Orchestrator(
                config,
                retriever,
                scorer,
                ranker,
                selector,
                packer,
                answerService,
                inMemoryTrace,
                traceEmitter,
                stageProgressReporter);

            var execution = await orchestrator
                .ExecuteAsync(scenarioId, queryText, repeat)
                .ConfigureAwait(false);

            for (var i = 0; i < execution.Runs.Count; i++)
            {
                var run = execution.Runs[i];
                WriteRun3Summary(screenLogger, run.Result, i + 1, repeat);
            }

            return ExitOk;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "run3 failed: {Message}", ex.Message);
            return ExitUsage;
        }
    }

    public async Task<int> Run4Async(string inputPath)
    {
        try
        {
            _logger
                .WithProperties(("input_path", inputPath))
                .Debug("Run4 command execution starting");

            var loader = new Phase4InputLoader();
            var dispatcher = new ScenarioEvaluatorDispatcher(new IScenarioEvaluator[]
            {
                new PolicyRefundEvaluator(logger: _logger),
                new Runbook502Evaluator(logger: _logger)
            }, _logger);
            var orchestrator = new Run4Orchestrator(dispatcher);
            var input = await loader.LoadAsync(inputPath).ConfigureAwait(false);
            var screenLogger = _screenLoggerFactory();
            var stageProgressReporter = CreateStageProgressReporter(screenLogger);
            var result = stageProgressReporter is null
                ? await orchestrator.ExecuteAsync(input, CancellationToken.None).ConfigureAwait(false)
                : await stageProgressReporter
                    .ExecuteStageAsync(
                        "Evaluating response...",
                        showSpinner: false,
                        cancellationToken => orchestrator.ExecuteAsync(input, cancellationToken),
                        CancellationToken.None)
                    .ConfigureAwait(false);

            screenLogger.Information("run_id={RunId}", result.RunId);
            screenLogger.Information("scenario_id={ScenarioId}", result.ScenarioId);
            screenLogger.Information("score_total={ScoreTotal}", result.ScoreTotal);

            switch (result.ScenarioResult)
            {
                case PolicyRefundScenarioResult policyResult:
                    screenLogger.Information(
                        "score_breakdown completeness_points={CompletenessPoints} format_points={FormatPoints} hallucination_penalty={HallucinationPenalty} accuracy_cap_applied={AccuracyCapApplied}",
                        policyResult.ScoreBreakdown.CompletenessPoints,
                        policyResult.ScoreBreakdown.FormatPoints,
                        policyResult.ScoreBreakdown.HallucinationPenalty,
                        policyResult.ScoreBreakdown.AccuracyCapApplied);
                    screenLogger.Information("missing_fact_labels={MissingFactLabels}", policyResult.MissingFactLabels);
                    screenLogger.Information("hallucination_flags={HallucinationFlags}", policyResult.HallucinationFlags);
                    break;
                case Runbook502ScenarioResult runbookResult:
                    screenLogger.Information(
                        "score_breakdown step_coverage_points={StepCoveragePoints} hallucination_penalty={HallucinationPenalty}",
                        runbookResult.ScoreBreakdown.StepCoveragePoints,
                        runbookResult.ScoreBreakdown.HallucinationPenalty);
                    screenLogger.Information("missing_step_labels={MissingStepLabels}", runbookResult.MissingStepLabels);
                    screenLogger.Information("order_violation_labels={OrderViolationLabels}", runbookResult.OrderViolationLabels);
                    break;
            }

            screenLogger.Information("query_suggestions={QuerySuggestions}", result.QuerySuggestions);

            return ExitOk;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "run4 failed: {Message}", ex.Message);
            return ExitUsage;
        }
    }

    public async Task<int> Run5Async(
        string scenarioId,
        string queryText,
        int repeat)
    {
        try
        {
            _logger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("repeat", repeat),
                    ("query_length", queryText.Length))
                .Debug("Run5 command execution starting");

            var screenLogger = _screenLoggerFactory();
            var stageProgressReporter = CreateStageProgressReporter(screenLogger);
            var renderer = _rendererFactory?.Invoke(screenLogger);
            var liveScreenEmitter = renderer is null ? null : new RendererTraceEmitter(renderer);

                var execution = await _run5Executor(
                    _logger,
                    _configuration,
                    scenarioId,
                    queryText,
                    repeat,
                        true,
                    liveScreenEmitter,
                    stageProgressReporter)
                .ConfigureAwait(false);

            await ScenarioRunner.WriteTraceArtifactsAsync(scenarioId, queryText, execution.Runs).ConfigureAwait(false);

            if (renderer is null)
            {
                for (var i = 0; i < execution.Runs.Count; i++)
                {
                    WriteRun5Summary(screenLogger, execution.Runs[i], i + 1, repeat, queryText);
                }
            }
            else
            {
                foreach (var run in execution.Runs)
                {
                    renderer.OnRunComplete(ScenarioRunner.BuildRunSummary(run));
                }
            }

            _logger
                .WithProperties(
                    ("scenario_id", scenarioId),
                    ("run_count", execution.Runs.Count))
                .Debug("Run5 command execution completed");

            return ExitOk;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "run5 failed: {Message}", ex.Message);
            return ExitUsage;
        }
    }

    private IStageProgressReporter? CreateStageProgressReporter()
    {
        return _stageProgressReporterFactory?.Invoke(_screenLoggerFactory());
    }

    private IStageProgressReporter? CreateStageProgressReporter(ILogger screenLogger)
    {
        return _stageProgressReporterFactory?.Invoke(screenLogger);
    }

    private static void WriteRunSummary(ILogger logger, RunResult result, int run, int repeat)
    {
        RetrievalSummaryRenderer.WriteSummary(logger, result, run, repeat, includeAnswer: false);
    }

    private static void WriteRun3Summary(ILogger logger, RunResult result, int run, int repeat)
    {
        RetrievalSummaryRenderer.WriteSummary(logger, result, run, repeat, includeAnswer: true);
    }

    private static void WriteRun5Summary(
        ILogger logger,
        Run5ExecutionRun run,
        int runIndex,
        int repeat,
        string queryText)
    {
        if (repeat > 1)
        {
            logger.Information("Run {Run}/{Repeat}", runIndex, repeat);
        }

        logger.Information("run_id={RunId}", run.Run1Result.RunId);
        logger.Information("scenario_id={ScenarioId}", run.Run1Result.RunRequestSnapshot.ScenarioId);
        logger.Information("run_mode=Run1");
        logger.Information("retrieval_queries={RetrievalQueries}", new[] { queryText });
        logger.Information("candidate_pool_size={Count}", run.Run1Result.RetrievalSummary.RetrievedCandidates.Count);
        logger.Information("selected_chunks={SelectedChunks}", TraceArtifactBuilder.BuildChunkList(run.Run1Result));
        logger.Information("context_chars={CharCount}", run.Run1Result.RetrievalSummary.ContextPack.CharCount);
        logger.Information("score_run1={Score}", run.Run1Evaluation.ScoreTotal);

        switch (run.Run1Evaluation.ScenarioResult)
        {
            case PolicyRefundScenarioResult policyResult:
                logger.Information("missing_fact_labels={MissingFactLabels}", policyResult.MissingFactLabels);
                logger.Information("query_suggestions={QuerySuggestions}", run.Run1Evaluation.QuerySuggestions);
                break;
            case Runbook502ScenarioResult runbookResult:
                logger.Information("missing_step_labels={MissingStepLabels}", runbookResult.MissingStepLabels);
                logger.Information("order_violation_labels={OrderViolationLabels}", runbookResult.OrderViolationLabels);
                logger.Information("query_suggestions={QuerySuggestions}", run.Run1Evaluation.QuerySuggestions);
                break;
        }

        if (run.Run2Result is not null && run.Run2Evaluation is not null)
        {
            logger.Information("run_id={RunId}", run.Run2Result.RunId);
            logger.Information("scenario_id={ScenarioId}", run.Run2Result.RunRequestSnapshot.ScenarioId);
            logger.Information("run_mode=Run2");
            logger.Information("retrieval_queries={RetrievalQueries}", run.Run2QuerySet!.AllQueries);
            logger.Information("candidate_pool_size={Count}", run.Run2Result.RetrievalSummary.RetrievedCandidates.Count);
            logger.Information("selected_chunks={SelectedChunks}", TraceArtifactBuilder.BuildChunkList(run.Run2Result));
            logger.Information("context_chars={CharCount}", run.Run2Result.RetrievalSummary.ContextPack.CharCount);
            logger.Information("score_run2={Score}", run.Run2Evaluation.ScoreTotal);

            switch (run.Run2Evaluation.ScenarioResult)
            {
                case PolicyRefundScenarioResult policyResult:
                    logger.Information("missing_fact_labels={MissingFactLabels}", policyResult.MissingFactLabels);
                    break;
                case Runbook502ScenarioResult runbookResult:
                    logger.Information("missing_step_labels={MissingStepLabels}", runbookResult.MissingStepLabels);
                    logger.Information("order_violation_labels={OrderViolationLabels}", runbookResult.OrderViolationLabels);
                    break;
            }

            logger.Information("score_delta={ScoreDelta}", run.ScoreDelta.ScoreDelta);
            logger.Information("memory_updates={MemoryUpdates}", run.MemoryUpdates);
        }

        logger.Information(string.Empty);
    }
}
