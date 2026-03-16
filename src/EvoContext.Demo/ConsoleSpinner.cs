using System.IO;

namespace EvoContext.Demo;

public sealed class ConsoleSpinner
{
    private static readonly string[] Frames =
    {
        "⠋",
        "⠙",
        "⠹",
        "⠸",
        "⠼",
        "⠴",
        "⠦",
        "⠧",
        "⠇",
        "⠏"
    };

    private readonly TextWriter _writer;
    private readonly TimeSpan _frameInterval;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<bool> _isOutputRedirected;

    public ConsoleSpinner(
        TextWriter? writer = null,
        TimeSpan? frameInterval = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        Func<bool>? isOutputRedirected = null)
    {
        _writer = writer ?? Console.Out;
        _frameInterval = frameInterval ?? TimeSpan.FromMilliseconds(100);
        _delayAsync = delayAsync ?? ((delay, cancellationToken) => Task.Delay(delay, cancellationToken));
        _isOutputRedirected = isOutputRedirected ?? (() => Console.IsOutputRedirected);
    }

    public bool IsInteractive => !_isOutputRedirected();

    public Task RunAsync(
        string message,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(operation);

        return RunAsync<object?>(
            message,
            async innerCancellationToken =>
            {
                await operation(innerCancellationToken).ConfigureAwait(false);
                return null;
            },
            cancellationToken);
    }

    public async Task<T> RunAsync<T>(
        string message,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(operation);

        if (!IsInteractive)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        using var spinnerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var spinnerTask = AnimateAsync(spinnerCancellation.Token);

        try
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            spinnerCancellation.Cancel();
            await AwaitAnimationAsync(spinnerTask).ConfigureAwait(false);
            ClearLine();
        }
    }

    private async Task AnimateAsync(CancellationToken cancellationToken)
    {
        var frameIndex = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            WriteFrame(Frames[frameIndex]);
            frameIndex = (frameIndex + 1) % Frames.Length;
            await _delayAsync(_frameInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private void WriteFrame(string frame)
    {
        _writer.Write($"\r{frame}");
        _writer.Flush();
    }

    private void ClearLine()
    {
        _writer.Write("\r \r");
        _writer.Flush();
    }

    private static async Task AwaitAnimationAsync(Task spinnerTask)
    {
        try
        {
            await spinnerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
