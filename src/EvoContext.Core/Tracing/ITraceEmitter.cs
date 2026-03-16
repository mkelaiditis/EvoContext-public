namespace EvoContext.Core.Tracing;

public interface ITraceEmitter
{
    Task EmitAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default);
}
