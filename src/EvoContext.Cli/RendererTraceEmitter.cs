using EvoContext.Core.Tracing;

namespace EvoContext.Cli;

public sealed class RendererTraceEmitter : ITraceEmitter
{
    private readonly IRunRenderer _renderer;

    public RendererTraceEmitter(IRunRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public Task EmitAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _renderer.OnEvent(traceEvent);
        return Task.CompletedTask;
    }
}
