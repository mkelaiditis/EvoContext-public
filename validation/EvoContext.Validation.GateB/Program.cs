using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EvoContext.Core.Config;
using EvoContext.Core.Documents;
using Microsoft.Extensions.Configuration;
using EvoContext.Infrastructure.Configuration;
using EvoContext.Infrastructure.Services;

const string ScenarioId = "policy_refund_v1";
const string Question = "What is the refund policy for annual subscriptions?";

const string SystemPrompt = "You are a helpful assistant. Answer only using the provided context. " +
                            "If the context does not contain the answer, say you do not have enough information.";

var exitCode = await RunAsync();
Environment.Exit(exitCode);

static async Task<int> RunAsync()
{
    try
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var config = GateAConfig.Load(
            configuration["QDRANT_URL"],
            configuration["QDRANT_API_KEY"],
            $"evocontext_{ScenarioId}");
        var phase0 = new CoreConfigLoader(configuration).Load();
        var basePath = Directory.GetCurrentDirectory();
        var scenario = new ScenarioLoader(basePath).Load(ScenarioId);
        var policyFolder = ResolveScenarioPath(basePath, scenario.DatasetPath);

        var loader = new MarkdownDocumentLoader();
        var documents = await loader.LoadAsync(policyFolder).ConfigureAwait(false);
        if (documents.Count != 8)
        {
            return WriteInfrastructureError(
                $"Expected 8 policy documents but found {documents.Count}.");
        }

        var chunker = new Chunker(phase0.ChunkSizeChars, phase0.ChunkOverlapChars);
        var chunks = new List<DocumentChunk>();

        foreach (var document in documents)
        {
            chunks.AddRange(chunker.Chunk(document));
        }

        var embeddingService = new EmbeddingService(phase0, configuration["OPENAI_API_KEY"]);
        var vectors = await embeddingService
            .EmbedBatchAsync(chunks.Select(chunk => chunk.Text).ToList())
            .ConfigureAwait(false);
        var vectorSize = vectors[0].Values.Count;

        var indexService = new QdrantIndexService(
            config.Host,
            config.Port,
            config.UseHttps,
            config.ApiKey,
            config.CollectionName);
        try
        {
            await indexService.CountAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return WriteInfrastructureError($"Qdrant unavailable: {ex.Message}");
        }
        await indexService.RecreateCollectionAsync(vectorSize).ConfigureAwait(false);
        await indexService.UpsertAsync(vectors, chunks).ConfigureAwait(false);

        var queryEmbedding = await embeddingService.EmbedAsync(Question).ConfigureAwait(false);
        var queryVector = new ReadOnlyMemory<float>(queryEmbedding.Values.ToArray());
        var retrievalService = new RetrievalService(
            config.Host,
            config.Port,
            config.UseHttps,
            config.ApiKey,
            config.CollectionName,
            phase0,
            embeddingService);
        var results = await retrievalService.SearchAsync(queryVector, phase0.RetrievalN)
            .ConfigureAwait(false);
        var rankedResults = results
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.DocId, StringComparer.Ordinal)
            .ThenBy(result => result.ChunkIndex)
            .ToList();

        if (rankedResults.Count < phase0.SelectionK)
        {
            return WriteInfrastructureError(
            $"Retrieval returned {rankedResults.Count} chunks, fewer than required {phase0.SelectionK}.");
        }

        var topChunks = rankedResults.Take(phase0.SelectionK)
            .Select(result => chunks.First(chunk => chunk.DocumentId == result.DocId && chunk.ChunkIndex == result.ChunkIndex))
            .ToList();

        if (topChunks.Count < phase0.SelectionK)
        {
            return WriteInfrastructureError(
            $"Selected {topChunks.Count} chunks, fewer than required {phase0.SelectionK}.");
        }

        var contextBuilder = new ContextPackBuilder(phase0.ContextBudgetChars);
        var contextPack = contextBuilder.Build(topChunks);

        if (contextPack.DocIds.Contains("06", StringComparer.Ordinal))
        {
            return WriteInfrastructureError("Doc 6 was present in the selected context.");
        }

        var generationService = new GenerationService(phase0, configuration["OPENAI_API_KEY"]);
        var runResults = new List<RunDetectionResult>(20);
        var detectionPatterns = BuildDetectionPatterns();
        var anchorPatterns = BuildAnchorPatterns();

        for (var run = 1; run <= 20; run++)
        {
            var userPrompt = BuildUserPrompt(contextPack.Content);
            var answer = await generationService
                .GenerateAnswerAsync(SystemPrompt, userPrompt)
                .ConfigureAwait(false);
            var detection = DetectF2(answer, contextPack.Content, detectionPatterns, anchorPatterns);
            runResults.Add(new RunDetectionResult(run, detection));
        }

        WriteOutput(contextPack, runResults, phase0);

        var hallucinationCount = runResults.Count(result => result.Detection.Hallucinated);
        var passed = hallucinationCount == 0;
        var exitCode = passed ? 0 : 2;

        Console.WriteLine();
        Console.WriteLine(passed ? "Gate B Result: PASS" : "Gate B Result: FAIL");
        if (!passed)
        {
            Console.WriteLine("Next action: tighten the system prompt and re-run without changing detection rules.");
            Console.WriteLine("If hallucinations persist: add grounding enforcement that facts only count when anchor phrases appear in context.");
        }

        return exitCode;
    }
    catch (Exception ex)
    {
        return WriteInfrastructureError(ex.Message);
    }
}

static string ResolveScenarioPath(string basePath, string datasetPath)
{
    return Path.IsPathRooted(datasetPath)
        ? datasetPath
        : Path.GetFullPath(Path.Combine(basePath, datasetPath));
}

static string BuildUserPrompt(string context)
{
    var builder = new StringBuilder();
    builder.AppendLine("Question:");
    builder.AppendLine(Question);
    builder.AppendLine();
    builder.AppendLine("Context:");
    builder.AppendLine(context);
    return builder.ToString();
}

static IReadOnlyList<Regex> BuildDetectionPatterns()
{
    return new List<Regex>
    {
        new Regex(@"\bprorated\b", RegexOptions.IgnoreCase),
        new Regex(@"\bunused\s+service\s+value\b", RegexOptions.IgnoreCase),
        new Regex(@"\bearly\s+termination\b", RegexOptions.IgnoreCase),
        new Regex(@"\bcommitment\s+term\b", RegexOptions.IgnoreCase),
        new Regex(@"\bcontract\s+year\b", RegexOptions.IgnoreCase)
    };
}

static IReadOnlyList<Regex> BuildAnchorPatterns()
{
    return new List<Regex>
    {
        new Regex(@"\bprorated\s+reimbursement\b", RegexOptions.IgnoreCase),
        new Regex(@"\bunused\s+service\s+value\b", RegexOptions.IgnoreCase),
        new Regex(@"\bservice\s+commitment\s+term\b", RegexOptions.IgnoreCase),
        new Regex(@"\bcontract\s+year\s+termination\b", RegexOptions.IgnoreCase)
    };
}

static DetectionResult DetectF2(
    string answer,
    string context,
    IReadOnlyList<Regex> detectionPatterns,
    IReadOnlyList<Regex> anchorPatterns)
{
    var matchedPattern = detectionPatterns.FirstOrDefault(pattern => pattern.IsMatch(answer));
    if (matchedPattern is null)
    {
        return new DetectionResult(false, null, false);
    }

    var anchorPresent = anchorPatterns.Any(pattern => pattern.IsMatch(context));
    return new DetectionResult(true, matchedPattern.ToString(), !anchorPresent);
}

static void WriteOutput(ContextPack contextPack, IReadOnlyList<RunDetectionResult> runResults, CoreConfigSnapshot phase0)
{
    var hallucinationCount = runResults.Count(result => result.Detection.Hallucinated);
    var hallucinationRate = runResults.Count == 0
        ? 0
        : (double)hallucinationCount / runResults.Count;
    var orderedDocIds = contextPack.DocIds
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToList();

    Console.WriteLine("Gate B Validation Results");
    Console.WriteLine($"Model: {phase0.GenerationModel}");
    Console.WriteLine($"Temperature: {phase0.Temperature}");
    Console.WriteLine("Run Count: 20");
    Console.WriteLine($"Question: {Question}");
    Console.WriteLine($"Context Doc IDs: {string.Join(", ", orderedDocIds)}");
    Console.WriteLine();

    foreach (var runResult in runResults)
    {
        var detected = runResult.Detection.PatternMatched ? "yes" : "no";
        var pattern = runResult.Detection.PatternMatched
            ? runResult.Detection.MatchedPattern
            : "n/a";

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Run {runResult.RunNumber,2}: F2 detected={detected} pattern={pattern}"));
    }

    Console.WriteLine();
    Console.WriteLine($"Total Runs: {runResults.Count}");
    Console.WriteLine($"F2 Hallucination Count: {hallucinationCount}");
    Console.WriteLine(
        string.Create(
            CultureInfo.InvariantCulture,
            $"Hallucination Rate: {hallucinationRate:P1}"));
}

static int WriteInfrastructureError(string message)
{
    Console.WriteLine("Gate B Infrastructure Error");
    Console.WriteLine(message);
    return 3;
}

internal sealed record DetectionResult(bool PatternMatched, string? MatchedPattern, bool Hallucinated);

internal sealed record RunDetectionResult(int RunNumber, DetectionResult Detection);