using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;

namespace EvoContext.Infrastructure.Services;

public sealed record Run3ExecutionRun(RunResult Result, IReadOnlyList<TraceEvent> Events);
