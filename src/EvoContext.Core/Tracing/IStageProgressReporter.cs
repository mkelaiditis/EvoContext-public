namespace EvoContext.Core.Tracing;

public interface IStageProgressReporter
{
    bool IsInteractive { get; }

    void ExecuteStage(string stageMessage, Action action);

    T ExecuteStage<T>(string stageMessage, Func<T> action);

    Task ExecuteStageAsync(
        string stageMessage,
        bool showSpinner,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteStageAsync<T>(
        string stageMessage,
        bool showSpinner,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}