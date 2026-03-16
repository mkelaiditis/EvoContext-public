using Serilog;

namespace EvoContext.Core.Logging;

public static class StructuredLogging
{
    public static ILogger NullLogger { get; } = new LoggerConfiguration().CreateLogger();

    public static ILogger WithProperties(this ILogger logger, params (string Name, object? Value)[] properties)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var current = logger;
        foreach (var (name, value) in properties)
        {
            current = current.ForContext(name, value, destructureObjects: false);
        }

        return current;
    }
}