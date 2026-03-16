using System.IO;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

public sealed class VerificationArtifactLocatorTests
{
    [Fact]
    public void TryFindNewTraceArtifact_IgnoresVerificationEvidenceFiles()
    {
        var locator = new VerificationArtifactLocator();
        var scenarioId = "phase11_locator_" + Path.GetRandomFileName().Replace(".", string.Empty, StringComparison.Ordinal);
        var scenarioDirectory = locator.GetScenarioTraceDirectory(scenarioId);
        Directory.CreateDirectory(scenarioDirectory);

        try
        {
            var snapshot = locator.CreateTraceArtifactSnapshot(scenarioId);
            var runId = scenarioId + "_20990101T000000Z_abcd";
            var traceArtifactPath = Path.Combine(scenarioDirectory, runId + ".json");
            var verificationEvidencePath = Path.Combine(scenarioDirectory, runId + ".verification.json");

            File.WriteAllText(traceArtifactPath, "{}");
            File.SetLastWriteTimeUtc(traceArtifactPath, new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            File.WriteAllText(verificationEvidencePath, "{}");
            File.SetLastWriteTimeUtc(verificationEvidencePath, new DateTime(2099, 1, 1, 0, 0, 1, DateTimeKind.Utc));

            var found = locator.TryFindNewTraceArtifact(snapshot, out var discovery, out var error);

            Assert.True(found, error);
            Assert.NotNull(discovery);
            Assert.Equal(runId, discovery!.RunId);
            Assert.Equal(traceArtifactPath, discovery.TraceArtifactPath);
            Assert.Equal(locator.BuildVerificationEvidencePath(scenarioId, runId), discovery.VerificationEvidencePath);
        }
        finally
        {
            if (Directory.Exists(scenarioDirectory))
            {
                Directory.Delete(scenarioDirectory, recursive: true);
            }
        }
    }
}