namespace EvoContext.Cli.Utilities;

public static class CliArgumentParser
{
    public static (string? ScenarioId, string? DatasetOverride) ParseScenarioDatasetArgs(string[] args)
    {
        string? scenarioId = null;
        string? datasetOverride = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scenario" when i + 1 < args.Length:
                    scenarioId = args[++i];
                    break;
                case "--dataset" when i + 1 < args.Length:
                    datasetOverride = args[++i];
                    break;
            }
        }

        return (scenarioId, datasetOverride);
    }

    public static (string? ScenarioId, string? QueryText, int Repeat) ParseRunWithRepeatArgs(string[] args)
    {
        string? scenarioId = null;
        string? queryText = null;
        var repeat = 1;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scenario" when i + 1 < args.Length:
                    scenarioId = args[++i];
                    break;
                case "--query" when i + 1 < args.Length:
                    queryText = args[++i];
                    break;
                case "--repeat" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed):
                    repeat = Math.Max(1, parsed);
                    i++;
                    break;
            }
        }

        return (scenarioId, queryText, repeat);
    }

    public static string? ParseRun4InputPath(string[] args)
    {
        string? inputPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--input" && i + 1 < args.Length)
            {
                inputPath = args[++i];
            }
        }

        return inputPath;
    }

    public static (string? ScenarioId, string? QueryText, int Repeat) ParseRun5Args(string[] args)
    {
        string? scenarioId = null;
        string? queryText = null;
        var repeat = 1;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scenario" when i + 1 < args.Length:
                    scenarioId = args[++i];
                    break;
                case "--query" when i + 1 < args.Length:
                    queryText = args[++i];
                    break;
                case "--repeat" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed):
                    repeat = Math.Max(1, parsed);
                    i++;
                    break;
            }
        }

        return (scenarioId, queryText, repeat);
    }

    public static (string? ScenarioId, string? QueryText, string? Mode, int Repeat) ParseRunArgs(string[] args)
    {
        string? scenarioId = null;
        string? queryText = null;
        string? mode = null;
        var repeat = 1;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scenario" when i + 1 < args.Length:
                    scenarioId = args[++i];
                    break;
                case "--query" when i + 1 < args.Length:
                    queryText = args[++i];
                    break;
                case "--mode" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--repeat" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed):
                    repeat = Math.Max(1, parsed);
                    i++;
                    break;
            }
        }

        return (scenarioId, queryText, mode, repeat);
    }

    public static string? ParseReplayRunId(string[] args)
    {
        string? runId = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--run-id" && i + 1 < args.Length)
            {
                runId = args[++i];
            }
        }

        return runId;
    }

    public static string? ParseStatsScenarioId(string[] args)
    {
        string? scenarioId = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--scenario" && i + 1 < args.Length)
            {
                scenarioId = args[++i];
            }
        }

        return scenarioId;
    }
}
