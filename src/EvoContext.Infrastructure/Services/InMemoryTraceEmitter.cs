using System.Collections.Generic;
using EvoContext.Core.Tracing;

namespace EvoContext.Infrastructure.Services;

public sealed class InMemoryTraceEmitter : ICapturingTraceEmitter
{
    private readonly List<TraceEvent> _events = new();

    public IReadOnlyList<TraceEvent> Events => _events;

    public Task EmitAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (traceEvent is null)
        {
            throw new ArgumentNullException(nameof(traceEvent));
        }

        _events.Add(traceEvent);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _events.Clear();
    }
}
