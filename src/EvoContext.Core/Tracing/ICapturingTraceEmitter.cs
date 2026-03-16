using System.Collections.Generic;

namespace EvoContext.Core.Tracing;

public interface ICapturingTraceEmitter : ITraceEmitter
{
    IReadOnlyList<TraceEvent> Events { get; }
    void Clear();
}
