using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal sealed record EnvironmentVariableRequirement(string Key, bool Required, string Description);

internal sealed record EnvironmentPrerequisiteResult(
    string RepositoryRoot,
    string CliProjectPath,
    string ResolvedEnvironmentName,
    string RuntimeDescription,
    IReadOnlyList<string> AvailableKeys,
    IReadOnlyList<string> MissingRequiredKeys,
    IReadOnlyList<string> MissingOptionalKeys)
{
    public bool IsSatisfied => MissingRequiredKeys.Count == 0;
}

internal sealed class EnvironmentPrerequisiteValidator
{
    private static readonly IReadOnlyList<EnvironmentVariableRequirement> Requirements =
    [
        new("OPENAI_API_KEY", true, "OpenAI API key used by the CLI for live embeddings and generation."),
        new("QDRANT_URL", true, "Qdrant endpoint used by the CLI."),
        new("QDRANT_API_KEY", false, "Optional Qdrant API key for authenticated deployments.")
    ];

    public EnvironmentPrerequisiteResult Validate()
    {
        var resolvedValues = ManualIntegrationConfiguration.ResolveForwardedValues();
        var availableKeys = new List<string>();
        var missingRequiredKeys = new List<string>();
        var missingOptionalKeys = new List<string>();

        foreach (var requirement in Requirements)
        {
            if (resolvedValues.ContainsKey(requirement.Key))
            {
                availableKeys.Add(requirement.Key);
                continue;
            }

            if (requirement.Required)
            {
                missingRequiredKeys.Add(requirement.Key);
            }
            else
            {
                missingOptionalKeys.Add(requirement.Key);
            }
        }

        return new EnvironmentPrerequisiteResult(
            ManualIntegrationWorkspace.RepoRoot,
            ManualIntegrationWorkspace.CliProjectPath,
            ManualIntegrationConfiguration.ResolveEnvironmentName(),
            RuntimeInformation.FrameworkDescription,
            availableKeys.OrderBy(static key => key, StringComparer.Ordinal).ToArray(),
            missingRequiredKeys.OrderBy(static key => key, StringComparer.Ordinal).ToArray(),
            missingOptionalKeys.OrderBy(static key => key, StringComparer.Ordinal).ToArray());
    }
}