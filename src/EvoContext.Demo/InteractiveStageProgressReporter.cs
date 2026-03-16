using EvoContext.Core.Tracing;
using Serilog;

namespace EvoContext.Demo;

public sealed class InteractiveStageProgressReporter : IStageProgressReporter
{
    private readonly ILogger _screenLogger;
    private readonly ConsoleSpinner _spinner;

    public InteractiveStageProgressReporter(ILogger screenLogger, ConsoleSpinner? spinner = null)
    {
        _screenLogger = screenLogger ?? throw new ArgumentNullException(nameof(screenLogger));
        _spinner = spinner ?? new ConsoleSpinner();
    }

    public bool IsInteractive => _spinner.IsInteractive;

    public void ExecuteStage(string stageMessage, Action action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageMessage);
        ArgumentNullException.ThrowIfNull(action);

        Announce(stageMessage);
        action();
    }

    public T ExecuteStage<T>(string stageMessage, Func<T> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageMessage);
        ArgumentNullException.ThrowIfNull(action);

        Announce(stageMessage);
        return action();
    }

    public Task ExecuteStageAsync(
        string stageMessage,
        bool showSpinner,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageMessage);
        ArgumentNullException.ThrowIfNull(action);

        Announce(stageMessage);
        return showSpinner
            ? _spinner.RunAsync(stageMessage, action, cancellationToken)
            : action(cancellationToken);
    }

    public Task<T> ExecuteStageAsync<T>(
        string stageMessage,
        bool showSpinner,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageMessage);
        ArgumentNullException.ThrowIfNull(action);

        Announce(stageMessage);
        return showSpinner
            ? _spinner.RunAsync(stageMessage, action, cancellationToken)
            : action(cancellationToken);
    }

    private void Announce(string stageMessage)
    {
        if (!IsInteractive)
        {
            return;
        }

        _screenLogger.Information(stageMessage);
    }
}
