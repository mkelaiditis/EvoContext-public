using System.Text.Json;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Infrastructure.Services;

public sealed class TraceArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _basePath;

    public TraceArtifactWriter(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            throw new ArgumentException("Base path is required.", nameof(basePath));
        }

        _basePath = basePath;
    }

    public async Task WriteAsync(TraceArtifact artifact, CancellationToken cancellationToken = default)
    {
        if (artifact is null)
        {
            throw new ArgumentNullException(nameof(artifact));
        }

        var scenarioDirectory = Path.Combine(_basePath, "artifacts", "traces", artifact.ScenarioId);
        Directory.CreateDirectory(scenarioDirectory);

        var outputPath = Path.Combine(scenarioDirectory, $"{artifact.RunId}.json");
        var tempPath = Path.Combine(scenarioDirectory, $"{artifact.RunId}.json.tmp");

        var json = JsonSerializer.Serialize(artifact, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, outputPath, true);
    }
}
