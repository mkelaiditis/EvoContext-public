using System.Collections.Concurrent;
using EvoContext.Core.Tracing;
using Serilog;
using Serilog.Formatting.Compact;

namespace EvoContext.Infrastructure.Services;

public sealed class TraceEmitter : ITraceEmitter, IDisposable
{
    private static readonly ISet<string> RedactedMetadataFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "raw_model_output",
        "answer",
        "score_total",
        "score_run1",
        "score_run2",
        "score_delta"
    };

    private readonly string _outputDirectory;
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new(StringComparer.Ordinal);
    private bool _disposed;

    public TraceEmitter(string? outputDirectory = null)
    {
        _outputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(Directory.GetCurrentDirectory(), "traces")
            : outputDirectory;
        Directory.CreateDirectory(_outputDirectory);
    }

    public Task EmitAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
    {
        if (traceEvent is null)
        {
            throw new ArgumentNullException(nameof(traceEvent));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var logger = _loggers.GetOrAdd(traceEvent.RunId, CreateLogger);
        var payload = new Dictionary<string, object?>
        {
            ["event_type"] = traceEvent.EventType.ToString(),
            ["run_id"] = traceEvent.RunId,
            ["scenario_id"] = traceEvent.ScenarioId,
            ["sequence_index"] = traceEvent.SequenceIndex,
            ["timestamp_utc"] = traceEvent.TimestampUtc?.ToString("O"),
            ["metadata"] = SanitizeMetadata(traceEvent.Metadata)
        };

        logger.Information("{@trace}", payload);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var logger in _loggers.Values)
        {
            if (logger is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _disposed = true;
    }

    private static IReadOnlyDictionary<string, object?> SanitizeMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in metadata)
        {
            if (RedactedMetadataFields.Contains(key))
            {
                continue;
            }

            sanitized[key] = value;
        }

        return sanitized;
    }

    private ILogger CreateLogger(string runId)
    {
        var filePath = Path.Combine(_outputDirectory, $"{runId}.jsonl");
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(new CompactJsonFormatter(), filePath)
            .CreateLogger();
    }
}
