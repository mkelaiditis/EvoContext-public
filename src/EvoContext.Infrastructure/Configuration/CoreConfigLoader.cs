using EvoContext.Core.Config;
using Microsoft.Extensions.Configuration;

namespace EvoContext.Infrastructure.Configuration;

public sealed class CoreConfigLoader
{
    private const string ExpectedEmbeddingModel = "text-embedding-3-small";
    private const string ExpectedGenerationModel = "gpt-4.1";
    private const double ExpectedTemperature = 0;
    private const double ExpectedTopP = 1;
    private const int ExpectedMaxTokens = 350;
    private const string ExpectedDistanceMetric = "cosine";
    private const int ExpectedChunkSizeChars = 1200;
    private const int ExpectedChunkOverlapChars = 200;
    private const int ExpectedRetrievalN = 10;
    private const int ExpectedSelectionK = 3;
    private const int ExpectedContextBudgetChars = 2200;
    private const string ExpectedGateATargetDocId = "06";

    private readonly IConfiguration _configuration;

    public CoreConfigLoader(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public CoreConfigSnapshot Load()
    {
        var snapshot = _configuration.GetSection("Phase0").Get<CoreConfigSnapshot>();
        if (snapshot is null)
        {
            throw new InvalidOperationException("Phase0 configuration section is missing or invalid.");
        }

        ValidateLock(snapshot);

        return snapshot;
    }

    private static void ValidateLock(CoreConfigSnapshot snapshot)
    {
        if (!string.Equals(snapshot.EmbeddingModel, ExpectedEmbeddingModel, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: EmbeddingModel must be {ExpectedEmbeddingModel}.");
        }

        if (!string.Equals(snapshot.GenerationModel, ExpectedGenerationModel, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: GenerationModel must be {ExpectedGenerationModel}.");
        }

        if (snapshot.Temperature != ExpectedTemperature)
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: Temperature must be {ExpectedTemperature}.");
        }

        if (snapshot.TopP != ExpectedTopP)
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: TopP must be {ExpectedTopP}.");
        }

        if (snapshot.MaxTokens != ExpectedMaxTokens)
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: MaxTokens must be {ExpectedMaxTokens}.");
        }

        if (!string.Equals(snapshot.DistanceMetric, ExpectedDistanceMetric, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: DistanceMetric must be {ExpectedDistanceMetric}.");
        }

        if (snapshot.ChunkSizeChars != ExpectedChunkSizeChars)
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: ChunkSizeChars must be {ExpectedChunkSizeChars}.");
        }

        if (snapshot.ChunkOverlapChars != ExpectedChunkOverlapChars)
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: ChunkOverlapChars must be {ExpectedChunkOverlapChars}.");
        }

        if (snapshot.RetrievalN != ExpectedRetrievalN)
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: RetrievalN must be {ExpectedRetrievalN}.");
        }

        if (snapshot.SelectionK != ExpectedSelectionK)
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: SelectionK must be {ExpectedSelectionK}.");
        }

        if (snapshot.ContextBudgetChars != ExpectedContextBudgetChars)
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: ContextBudgetChars must be {ExpectedContextBudgetChars}.");
        }

        if (!string.Equals(snapshot.GateATargetDocId, ExpectedGateATargetDocId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Phase 0 lock violation: GateATargetDocId must be {ExpectedGateATargetDocId}.");
        }
    }
}
