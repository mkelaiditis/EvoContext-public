using System.Collections.Generic;
using EvoContext.Core.Tracing;

namespace EvoContext.Infrastructure.Services;

public sealed class CompositeTraceEmitter : ITraceEmitter, IDisposable
{
    private readonly IReadOnlyList<ITraceEmitter> _emitters;
    private bool _disposed;

    public CompositeTraceEmitter(params ITraceEmitter[] emitters)
    {
        _emitters = emitters ?? throw new ArgumentNullException(nameof(emitters));
    }

    public async Task EmitAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
    {
        if (traceEvent is null)
        {
            throw new ArgumentNullException(nameof(traceEvent));
        }

        foreach (var emitter in _emitters)
        {
            await emitter.EmitAsync(traceEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var emitter in _emitters)
        {
            if (emitter is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _disposed = true;
    }
}
