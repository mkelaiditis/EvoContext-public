using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EvoContext.Core.Config;
using EvoContext.Core.Embeddings;
using EvoContext.Core.Logging;
using OpenAI.Embeddings;
using Serilog;

namespace EvoContext.Infrastructure.Services;

public sealed class EmbeddingService : IEmbedder
{
    private readonly EmbeddingClient _client;
    private readonly CoreConfigSnapshot _config;
    private readonly ILogger _logger;

    public EmbeddingService(CoreConfigSnapshot config, string? apiKey = null, ILogger? logger = null)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        _config = config;
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<EmbeddingService>();

        apiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY is required for embedding generation.");
        }

        _client = new EmbeddingClient(config.EmbeddingModel, apiKey);
    }

    public async Task<EmbeddingVector> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Embedding input must be non-empty.", nameof(text));
        }

        cancellationToken.ThrowIfCancellationRequested();
        OpenAIEmbedding embedding = await _client
            .GenerateEmbeddingAsync(text, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var values = embedding.ToFloats().ToArray();

        _logger
            .WithProperties(
                ("embedding_model", _config.EmbeddingModel),
                ("input_length", text.Length),
                ("vector_dimension", values.Length))
            .Debug("Embedding request completed");

        return new EmbeddingVector(BuildVectorId(text), text, values);
    }

    public async Task<IReadOnlyList<EmbeddingVector>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts is null)
        {
            throw new ArgumentNullException(nameof(texts));
        }

        var filtered = texts.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        if (filtered.Count == 0)
        {
            throw new ArgumentException("Embedding inputs must be non-empty.", nameof(texts));
        }

        var results = new List<EmbeddingVector>(filtered.Count);
        foreach (var input in filtered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenAIEmbedding embedding = await _client
                .GenerateEmbeddingAsync(input, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            results.Add(new EmbeddingVector(BuildVectorId(input), input, embedding.ToFloats().ToArray()));
        }

        _logger
            .WithProperties(
                ("embedding_model", _config.EmbeddingModel),
                ("input_count", filtered.Count),
                ("vector_dimension", results[0].Values.Count))
            .Debug("Embedding batch completed");

        return results;
    }

    private static string BuildVectorId(string sourceText)
    {
        var bytes = Encoding.UTF8.GetBytes(sourceText);
        var hash = SHA256.HashData(bytes);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}
