namespace EvoContext.Core.Runs;

public interface IRunExecutor
{
    Task<RunResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default);
}
