using System.IO;
using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests;

public sealed class ScenarioLoaderTests
{
    [Fact]
    public void Load_ReturnsScenarioDefinition_ForPolicyRefundScenario()
    {
        var loader = new ScenarioLoader(TestDatasetPaths.RepoRoot);

        var scenario = loader.Load("policy_refund_v1");

        Assert.Equal("policy_refund_v1", scenario.ScenarioId);
        Assert.Equal("data/scenarios/policy_refund_v1/documents", scenario.DatasetPath);

        var resolvedDatasetPath = Path.Combine(TestDatasetPaths.RepoRoot, scenario.DatasetPath);
        Assert.True(Directory.Exists(resolvedDatasetPath));
    }

    [Fact]
    public void Load_Throws_WhenDatasetPathDoesNotExist()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, ".git"));

        var scenarioRoot = Path.Combine(temp.Path, "data", "scenarios", "broken_scenario");
        Directory.CreateDirectory(scenarioRoot);

        var scenarioJsonPath = Path.Combine(scenarioRoot, "scenario.json");
        File.WriteAllText(scenarioJsonPath, """
{
  "scenario_id": "broken_scenario",
  "display_name": "Broken Scenario",
  "dataset_path": "data/scenarios/broken_scenario/documents",
  "primary_query": "test query",
  "fallback_queries": [],
  "run_mode_default": "run1",
  "demo_label": "Broken"
}
""");

        var loader = new ScenarioLoader(temp.Path);

        var exception = Assert.Throws<DirectoryNotFoundException>(() => loader.Load("broken_scenario"));
        Assert.Contains("Scenario dataset_path not found", exception.Message);
    }
}
