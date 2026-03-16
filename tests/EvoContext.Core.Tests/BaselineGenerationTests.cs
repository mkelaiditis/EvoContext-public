using System;
using System.IO;
using EvoContext.Core.Tests.Baselines;

namespace EvoContext.Core.Tests;

public sealed class BaselineGenerationTests
{
    [Fact]
    public void GenerateGateABaselineJson()
    {
        if (!IsBaselineGenerationEnabled())
        {
            Assert.Skip("Set EVOCONTEXT_GENERATE_BASELINE=1 to generate the baseline JSON.");
            return;
        }

        var repoRoot = TestDatasetPaths.RepoRoot;
        var datasetPath = TestDatasetPaths.PolicyDocsPath;
        var outputPath = GateAIngestionBaseline.BaselinePath;

        GateABaselineGenerator.Generate(repoRoot, datasetPath, outputPath);

        Assert.True(File.Exists(outputPath), "Baseline JSON was not written.");
    }

    private static bool IsBaselineGenerationEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("EVOCONTEXT_GENERATE_BASELINE"),
            "1",
            StringComparison.Ordinal);
    }
}
