using System;
using System.IO;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal static class ManualIntegrationWorkspace
{
    private static readonly Lazy<string> RepoRootLazy = new(FindRepoRoot);

    public static string RepoRoot => RepoRootLazy.Value;

    public static string CliProjectPath => Path.Combine(RepoRoot, "src", "EvoContext.Cli");

    public static string TraceRootPath => Path.Combine(RepoRoot, "artifacts", "traces");

    public static string OperationalTraceRootPath => Path.Combine(RepoRoot, "traces");

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "EvoContext.slnx"))
                || Directory.Exists(Path.Combine(current.FullName, ".git"))
                || Directory.Exists(Path.Combine(current.FullName, ".specify")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found from test base directory.");
    }
}