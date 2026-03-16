using Microsoft.Extensions.Configuration;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal static class ManualIntegrationConfiguration
{
    private static readonly string[] ForwardedKeys =
    [
        "OPENAI_API_KEY",
        "QDRANT_URL",
        "QDRANT_API_KEY"
    ];

    public static IConfigurationRoot Build()
    {
        var environmentName = ResolveEnvironmentName();

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddUserSecrets<PolicyRefundVerificationHarness>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    public static IReadOnlyDictionary<string, string> ResolveForwardedValues()
    {
        var configuration = Build();
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var key in ForwardedKeys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                resolved[key] = value;
            }
        }

        return resolved;
    }

    public static string ResolveEnvironmentName()
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        }

        return string.IsNullOrWhiteSpace(environmentName)
            ? "Production"
            : environmentName.Trim();
    }
}