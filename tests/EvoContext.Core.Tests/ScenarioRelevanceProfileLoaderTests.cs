using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests;

public sealed class ScenarioRelevanceProfileLoaderTests
{
    [Fact]
    public void Load_ReturnsProfile_ForPolicyRefundScenario()
    {
        var loader = new RelevanceProfileLoader(TestDatasetPaths.RepoRoot);

        var profile = loader.Load("policy_refund_v1");

        Assert.Equal(3, profile.K);
        Assert.Contains("02", profile.RelevantDocuments);
        Assert.Equal("06", profile.LabelToDocumentMap["F2"]);
    }

    [Fact]
    public void Load_Throws_WhenProfileMissing()
    {
        var scenarioId = "missing_profile_" + Guid.NewGuid().ToString("N")[..8];
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, ".git"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "data", "scenarios", scenarioId));

        var loader = new RelevanceProfileLoader(temp.Path);

        var ex = Assert.Throws<FileNotFoundException>(() => loader.Load(scenarioId));
        Assert.Contains("Relevance profile not found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_Throws_WhenRequiredFieldMissing()
    {
        const string scenarioId = "broken_relevance_profile";
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, ".git"));
        var scenarioRoot = Path.Combine(temp.Path, "data", "scenarios", scenarioId);
        Directory.CreateDirectory(scenarioRoot);

        File.WriteAllText(Path.Combine(scenarioRoot, "relevance_profile.json"), """
{
  "k": 3,
  "highly_relevant_documents": ["06"],
  "label_to_document_map": {"F1": "02"}
}
""");

        var loader = new RelevanceProfileLoader(temp.Path);

        var ex = Assert.Throws<InvalidDataException>(() => loader.Load(scenarioId));
        Assert.Contains("missing relevant_documents", ex.Message, StringComparison.Ordinal);
    }
}
