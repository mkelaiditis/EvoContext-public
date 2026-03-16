using EvoContext.Core.Runs;
using Serilog;

namespace EvoContext.Cli.Services;

public interface IRetrievalSummaryRenderer
{
    void WriteSummary(ILogger logger, RunResult result, int run, int repeat, bool includeAnswer);
}