using System.Collections.Generic;
using EvoContext.Core.Runs;
using EvoContext.Core.Tracing;

namespace EvoContext.Infrastructure.Services;

public sealed record Run1ExecutionRun(RunResult Result, IReadOnlyList<TraceEvent> Events);
