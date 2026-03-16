using EvoContext.Cli.Utilities;
using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Config;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Evidence;
using EvoContext.Core.Logging;
using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Configuration;
using EvoContext.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace EvoContext.Cli.Services;

public static class Run5ServiceFactory
{
    public static async Task<Run5ExecutionResult> ExecuteRun5Async(
        ILogger logger,
        IConfiguration configuration,
        string scenarioId,
        string queryText,
        int repeat,
        bool allowRun2,
        ITraceEmitter? liveScreenEmitter = null,
        IStageProgressReporter? stageProgressReporter = null)
    {
        logger
            .WithProperties(
                ("scenario_id", scenarioId),
                ("repeat", repeat),
                ("allow_run2", allowRun2),
                ("live_screen_emitter_enabled", liveScreenEmitter is not null))
            .Debug("Run5 service factory starting");

        var collectionName = CliPathResolver.BuildScenarioCollectionName(scenarioId);
        var gateConfig = GateAConfig.Load(
            configuration["QDRANT_URL"],
            configuration["QDRANT_API_KEY"],
            collectionName);
        var config = new CoreConfigLoader(configuration).Load();
        var embedder = new EmbeddingService(config, configuration["OPENAI_API_KEY"], logger);
        var retriever = new RetrievalService(
            gateConfig.Host,
            gateConfig.Port,
            gateConfig.UseHttps,
            gateConfig.ApiKey,
            gateConfig.CollectionName,
            config,
            embedder,
            logger);
        var scorer = new CandidateScorer();
        var ranker = new CandidateRanker();
        var selector = new ContextSelector();
        var packer = new ContextPackPacker(config.ContextBudgetChars);
        var generator = new GenerationService(config, configuration["OPENAI_API_KEY"], logger);
        var promptBuilder = new Phase3PromptBuilder();
        var validator = new AnswerFormatValidator();
        var answerService = new AnswerGenerationService(promptBuilder, generator, validator, logger);
        var evaluatorDispatcher = new ScenarioEvaluatorDispatcher(new IScenarioEvaluator[]
        {
            new PolicyRefundEvaluator(logger: logger),
            new Runbook502Evaluator(logger: logger)
        }, logger);
        var queryBuilder = new Run2QueryBuilder();
        var candidatePoolMerger = new CandidatePoolMerger();
        var run2CandidateScorer = new Run2CandidateScorer();
        var usefulnessStorePath = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", $"usefulness_memory_{scenarioId}.json");
        var usefulnessStore = new UsefulnessMemoryStore(usefulnessStorePath, logger);
        var evidenceExtractor = new DetectedEvidenceExtractor(
            Phase4RuleTables.FactRules,
            Phase4RuleTables.PresentLabelByFactId);
        var inMemoryTrace = new InMemoryTraceEmitter();
        IDisposable traceEmitterDisposable;
        ITraceEmitter orchestratorTraceEmitter;

        if (liveScreenEmitter is null)
        {
            var traceEmitter = new TraceEmitter();
            traceEmitterDisposable = traceEmitter;
            orchestratorTraceEmitter = traceEmitter;
        }
        else
        {
            var traceEmitter = new TraceEmitter();
            var compositeTraceEmitter = new CompositeTraceEmitter(traceEmitter, liveScreenEmitter);
            traceEmitterDisposable = compositeTraceEmitter;
            orchestratorTraceEmitter = compositeTraceEmitter;
        }

        using (traceEmitterDisposable)
        {
            var orchestrator = new Run5Orchestrator(
                config,
                retriever,
                scorer,
                run2CandidateScorer,
                ranker,
                selector,
                packer,
                queryBuilder,
                candidatePoolMerger,
                usefulnessStore,
                answerService,
                evaluatorDispatcher,
                inMemoryTrace,
                orchestratorTraceEmitter,
                logger,
                stageProgressReporter,
                evidenceExtractor);

            return await orchestrator
                .ExecuteAsync(scenarioId, queryText, repeat, allowRun2)
                .ConfigureAwait(false);
        }
    }
}
