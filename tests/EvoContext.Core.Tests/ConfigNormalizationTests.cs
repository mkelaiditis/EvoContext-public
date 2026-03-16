using System.Collections.Generic;
using EvoContext.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace EvoContext.Core.Tests;

public sealed class ConfigNormalizationTests
{
    [Fact]
    public void CoreConfigLoader_BindsPhase0Values()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Phase0:EmbeddingModel"] = "text-embedding-3-small",
                ["Phase0:GenerationModel"] = "gpt-4.1",
                ["Phase0:Temperature"] = "0",
                ["Phase0:TopP"] = "1",
                ["Phase0:MaxTokens"] = "350",
                ["Phase0:DistanceMetric"] = "cosine",
                ["Phase0:ChunkSizeChars"] = "1200",
                ["Phase0:ChunkOverlapChars"] = "200",
                ["Phase0:RetrievalN"] = "10",
                ["Phase0:SelectionK"] = "3",
                ["Phase0:ContextBudgetChars"] = "2200",
                ["Phase0:GateATargetDocId"] = "06"
            })
            .Build();

        var snapshot = new CoreConfigLoader(config).Load();

        Assert.Equal("text-embedding-3-small", snapshot.EmbeddingModel);
        Assert.Equal("gpt-4.1", snapshot.GenerationModel);
        Assert.Equal(0.0, snapshot.Temperature);
        Assert.Equal(1.0, snapshot.TopP);
        Assert.Equal(350, snapshot.MaxTokens);
        Assert.Equal("cosine", snapshot.DistanceMetric);
        Assert.Equal(1200, snapshot.ChunkSizeChars);
        Assert.Equal(200, snapshot.ChunkOverlapChars);
        Assert.Equal(10, snapshot.RetrievalN);
        Assert.Equal(3, snapshot.SelectionK);
        Assert.Equal(2200, snapshot.ContextBudgetChars);
        Assert.Equal("06", snapshot.GateATargetDocId);
    }
}
