using EvoContext.Cli.Utilities;
using EvoContext.Core.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace EvoContext.Demo;

internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitUsage = 2;
    private const string DefaultEnvironmentName = "Production";

    private static int Main(string[] args)
    {
        var configuration = BuildConfiguration();
        Log.Logger = CreateLogger(configuration);

        try
        {
            var logger = Log.Logger;
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp(logger);
                return ExitOk;
            }

            var command = args[0].ToLowerInvariant();
            var facade = new DemoHostFacade(logger, configuration);

            return command switch
            {
                "ingest" => facade.Ingest(args.Skip(1).ToArray()),
                "embed" => facade.Embed(args.Skip(1).ToArray()),
                "run" => facade.Run(args.Skip(1).ToArray()),
                "run1" => facade.Run1(args.Skip(1).ToArray()),
                "run3" => facade.Run3(args.Skip(1).ToArray()),
                "run4" => facade.Run4(args.Skip(1).ToArray()),
                "run5" => facade.Run5(args.Skip(1).ToArray()),
                "replay" => facade.Replay(args.Skip(1).ToArray()),
                "stats" => facade.Stats(args.Skip(1).ToArray()),
                _ => UnknownCommand(logger, command)
            };
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static int UnknownCommand(ILogger logger, string command)
    {
        logger.Error("Unknown command: {Command}", command);
        PrintHelp(logger);
        return ExitUsage;
    }

    private static bool IsHelp(string arg)
    {
        return string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintHelp(ILogger logger)
    {
        foreach (var line in CliHelpText.Text.Split('\n'))
        {
            logger.Information(line.TrimEnd('\r'));
        }
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var environmentName = ResolveEnvironmentName();

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddUserSecrets<DemoSecrets>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveEnvironmentName()
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        }

        return string.IsNullOrWhiteSpace(environmentName)
            ? DefaultEnvironmentName
            : environmentName.Trim();
    }

    private static ILogger CreateLogger(IConfiguration configuration)
    {
        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }
}

internal sealed class DemoSecrets
{
}
