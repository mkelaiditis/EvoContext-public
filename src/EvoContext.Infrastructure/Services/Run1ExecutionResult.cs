using System.Collections.Generic;

namespace EvoContext.Infrastructure.Services;

public sealed record Run1ExecutionResult(IReadOnlyList<Run1ExecutionRun> Runs);
