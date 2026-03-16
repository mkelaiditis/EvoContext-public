using System.Text.Json;
using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Logging;
using Serilog;

namespace EvoContext.Infrastructure.Services;

public sealed class UsefulnessMemoryStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _outputPath;
    private readonly ILogger _logger;

    public UsefulnessMemoryStore(string? outputPath = null, ILogger? logger = null)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
        _outputPath = string.IsNullOrWhiteSpace(outputPath)
            ? Path.Combine(directory, "usefulness_memory.json")
            : outputPath;
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<UsefulnessMemoryStore>();
        var outputDirectory = Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
    }

    public async Task<UsefulnessMemorySnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_outputPath))
        {
            _logger
                .WithProperties(
                    ("output_path", _outputPath),
                    ("file_found", false),
                    ("item_count", 0))
                .Debug("Usefulness memory loaded");

            return new UsefulnessMemorySnapshot(Array.Empty<UsefulnessMemoryItem>());
        }

        var json = await File.ReadAllTextAsync(_outputPath, cancellationToken).ConfigureAwait(false);
        UsefulnessMemorySnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<UsefulnessMemorySnapshot>(json, ReadOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Usefulness memory store at '{_outputPath}' contains invalid JSON.",
                ex);
        }
        if (snapshot is null)
        {
            throw new InvalidOperationException("Usefulness memory store is invalid or empty.");
        }

        _logger
            .WithProperties(
                ("output_path", _outputPath),
                ("file_found", true),
                ("item_count", snapshot.Items.Count))
            .Debug("Usefulness memory loaded");

        return snapshot;
    }

    public async Task SaveAsync(UsefulnessMemorySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var json = JsonSerializer.Serialize(snapshot, WriteOptions);
        await File.WriteAllTextAsync(_outputPath, json, cancellationToken).ConfigureAwait(false);

        _logger
            .WithProperties(
                ("output_path", _outputPath),
                ("item_count", snapshot.Items.Count))
            .Debug("Usefulness memory persisted");
    }
}
