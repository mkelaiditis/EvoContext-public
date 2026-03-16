using System;
using System.IO;

namespace EvoContext.Core.Tests;

internal static class TestDatasetPaths
{
    private static readonly Lazy<string> RepoRootLazy = new(FindRepoRoot);

    public static string RepoRoot => RepoRootLazy.Value;

    public static string PolicyDocsPath => Path.Combine(
        RepoRoot,
        "data",
        "scenarios",
        "policy_refund_v1",
        "documents");

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
