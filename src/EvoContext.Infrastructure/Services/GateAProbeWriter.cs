using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Infrastructure.Services;

public sealed class GateAProbeWriter : IGateAProbeWriter
{
    private readonly string _outputPath;

    public GateAProbeWriter(string? outputPath = null)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
        _outputPath = string.IsNullOrWhiteSpace(outputPath)
            ? Path.Combine(directory, "gate_a_probe.json")
            : outputPath;
        var outputDirectory = Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
    }

    public async Task WriteAsync(GateAProbeArtifact artifact, CancellationToken cancellationToken = default)
    {
        if (artifact is null)
        {
            throw new ArgumentNullException(nameof(artifact));
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(artifact, options);
        await File.WriteAllTextAsync(_outputPath, json, cancellationToken).ConfigureAwait(false);
    }
}
