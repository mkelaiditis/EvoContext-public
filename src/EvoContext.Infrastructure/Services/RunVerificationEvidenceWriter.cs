using System.Text.Json;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Infrastructure.Services;

public sealed class RunVerificationEvidenceWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _basePath;

    public RunVerificationEvidenceWriter(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            throw new ArgumentException("Base path is required.", nameof(basePath));
        }

        _basePath = basePath;
    }

    public async Task WriteAsync(RunVerificationEvidence evidence, CancellationToken cancellationToken = default)
    {
        if (evidence is null)
        {
            throw new ArgumentNullException(nameof(evidence));
        }

        var scenarioDirectory = Path.Combine(_basePath, "artifacts", "traces", evidence.ScenarioId);
        Directory.CreateDirectory(scenarioDirectory);

        var outputPath = Path.Combine(scenarioDirectory, $"{evidence.RunId}.verification.json");
        var tempPath = Path.Combine(scenarioDirectory, $"{evidence.RunId}.verification.json.tmp");

        var json = JsonSerializer.Serialize(evidence, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, outputPath, true);
    }
}