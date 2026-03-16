using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using EvoContext.Core.Documents;

namespace EvoContext.Core.Tests.Baselines;

internal static class GateABaselineGenerator
{
    private const string DatasetName = "policy_refund_v1";
    private const int ChunkSizeChars = 1200;
    private const int ChunkOverlapChars = 200;

    public static void Generate(string repoRoot, string datasetPath, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new ArgumentException("Repository root is required.", nameof(repoRoot));
        }

        if (!Directory.Exists(repoRoot))
        {
            throw new DirectoryNotFoundException($"Repository root not found: {repoRoot}");
        }

        if (string.IsNullOrWhiteSpace(datasetPath))
        {
            throw new ArgumentException("Dataset path is required.", nameof(datasetPath));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        if (!Directory.Exists(datasetPath))
        {
            throw new DirectoryNotFoundException($"Dataset folder not found: {datasetPath}");
        }

        var service = new DocumentIngestionService();
        var result = service
            .IngestAsync(datasetPath, ChunkSizeChars, ChunkOverlapChars, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        var chunkCounts = result.Chunks
            .GroupBy(chunk => chunk.DocumentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var documents = result.Documents
            .OrderBy(document => document.DocId, StringComparer.Ordinal)
            .Select(document => new BaselineDocument(
                document.DocId,
                document.NormalizedText.Length,
                chunkCounts.TryGetValue(document.DocId, out var count) ? count : 0))
            .ToList();

        var chunks = result.Chunks
            .OrderBy(chunk => chunk.DocumentId, StringComparer.Ordinal)
            .ThenBy(chunk => chunk.ChunkIndex)
            .Select(chunk => new BaselineChunk(
                chunk.DocumentId,
                chunk.ChunkIndex,
                chunk.ChunkId,
                chunk.StartChar,
                chunk.EndChar,
                chunk.Text))
            .ToList();

        var snapshot = new BaselineSnapshot(
            1,
            DatasetName,
            new BaselineChunking(ChunkSizeChars, ChunkOverlapChars),
            documents,
            chunks);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(snapshot, options);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, json, new UTF8Encoding(false));
    }
}
