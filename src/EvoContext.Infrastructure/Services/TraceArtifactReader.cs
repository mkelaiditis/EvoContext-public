using System.Text.Json;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Infrastructure.Services;

public sealed class TraceArtifactReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TraceArtifact Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Trace artifact path is required.", nameof(path));
        }

        var json = File.ReadAllText(path);
        var artifact = JsonSerializer.Deserialize<TraceArtifact>(json, JsonOptions);
        if (artifact is null)
        {
            throw new InvalidDataException($"Trace artifact could not be parsed: {path}");
        }

        return artifact;
    }

    public bool TryRead(string path, out TraceArtifact? artifact, out string? error)
    {
        try
        {
            artifact = Read(path);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            artifact = null;
            error = ex.Message;
            return false;
        }
    }
}
