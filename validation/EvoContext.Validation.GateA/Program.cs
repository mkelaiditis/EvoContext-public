using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EvoContext.Core.Config;
using EvoContext.Core.Documents;
using Microsoft.Extensions.Configuration;
using EvoContext.Infrastructure.Configuration;
using EvoContext.Infrastructure.Models;
using EvoContext.Infrastructure.Services;

const string ScenarioId = "policy_refund_v1";
const string Question = "What is the refund policy for annual subscriptions?";

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

		var chunker = new Chunker(phase0.ChunkSizeChars, phase0.ChunkOverlapChars);
		var chunks = new List<DocumentChunk>();
		var doc6ChunkCount = 0;

		foreach (var document in documents)
		{
			var documentChunks = chunker.Chunk(document);
			if (document.DocumentId == "06")
			{
				doc6ChunkCount = documentChunks.Count;
			}

			chunks.AddRange(documentChunks);
		}

		if (doc6ChunkCount == 0)
		{
			return WriteInfrastructureError("Doc 6 produced zero chunks.");
		}

		if (chunks.Count == 0)
		{
			return WriteInfrastructureError("No chunks were generated.");
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
		await indexService.RecreateCollectionAsync(vectorSize).ConfigureAwait(false);
		await indexService.UpsertAsync(vectors, chunks).ConfigureAwait(false);

		var indexedCount = await indexService.CountAsync().ConfigureAwait(false);
		if (indexedCount != (ulong)chunks.Count)
		{
			return WriteInfrastructureError(
				$"Indexed chunk count mismatch. Expected {chunks.Count} but got {indexedCount}.");
		}

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
			.Select((result, index) => new RetrievalResult(
				result.DocId,
				result.ChunkIndex,
				result.Score,
				index + 1))
			.ToList();

		Console.WriteLine("Gate A Validation Results");
		Console.WriteLine($"Question: {Question}");
		Console.WriteLine($"Results Returned: {rankedResults.Count}");
		Console.WriteLine();
		Console.WriteLine("Ranked Results:");

		foreach (var result in rankedResults)
		{
			Console.WriteLine(
				string.Create(
					CultureInfo.InvariantCulture,
					$"{result.Rank,2}. doc_id={result.DocId} chunk_id={result.ChunkIndex} score={result.Score:F6}"));
		}

		var doc6Result = rankedResults.FirstOrDefault(result => result.DocId == "06");
		var doc6RankDisplay = doc6Result is null ? ">10" : doc6Result.Rank.ToString(CultureInfo.InvariantCulture);
		var doc6ScoreDisplay = doc6Result is null
			? "n/a"
			: doc6Result.Score.ToString("F6", CultureInfo.InvariantCulture);

		Console.WriteLine();
		Console.WriteLine($"Doc 6 Rank: {doc6RankDisplay}");
		Console.WriteLine($"Doc 6 Score: {doc6ScoreDisplay}");

		var passed = doc6Result is null || doc6Result.Rank > phase0.SelectionK;
		var exitCode = passed ? 0 : 2;

		Console.WriteLine();
		Console.WriteLine(passed ? "Gate A Result: PASS" : "Gate A Result: FAIL");

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
static int WriteInfrastructureError(string message)
{
	Console.WriteLine("Gate A Infrastructure Error");
	Console.WriteLine(message);
	return 3;
}
