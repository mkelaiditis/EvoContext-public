using System.Text.Json.Serialization;

namespace EvoContext.Infrastructure.Models;

public sealed record RelevanceProfile(
    [property: JsonPropertyName("k")] int K,
    [property: JsonPropertyName("relevant_documents")] IReadOnlyList<string> RelevantDocuments,
    [property: JsonPropertyName("highly_relevant_documents")] IReadOnlyList<string>? HighlyRelevantDocuments,
    [property: JsonPropertyName("label_to_document_map")] IReadOnlyDictionary<string, string> LabelToDocumentMap);
