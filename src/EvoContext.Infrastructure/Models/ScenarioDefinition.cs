using System.Text.Json.Serialization;

namespace EvoContext.Infrastructure.Models;

public sealed record ScenarioDefinition(
    [property: JsonPropertyName("scenario_id")] string ScenarioId,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("dataset_path")] string DatasetPath,
    [property: JsonPropertyName("primary_query")] string PrimaryQuery,
    [property: JsonPropertyName("fallback_queries")] IReadOnlyList<string> FallbackQueries,
    [property: JsonPropertyName("run_mode_default")] string RunModeDefault,
    [property: JsonPropertyName("demo_label")] string DemoLabel);
