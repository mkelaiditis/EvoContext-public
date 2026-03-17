using System.Text.Json;

namespace EvoContext.Core.Tests.Cli;

internal static class StatsRetrievalDiagnosticsTestData
{
    internal static string BuildAvailableDiagnosticsJson()
    {
        var payload = new
        {
            retrieval_diagnostics = new
            {
                status = "available",
                k = 3,
                run1 = new
                {
                    top_k_documents = new[] { "02", "01", "03" },
                    hit_at_k = true,
                    recall_at_k = 0.4,
                    mrr = 1.0,
                    ndcg_at_k = 0.479
                },
                run2 = new
                {
                    top_k_documents = new[] { "02", "06", "05" },
                    hit_at_k = true,
                    recall_at_k = 0.6,
                    mrr = 1.0,
                    ndcg_at_k = 0.882
                },
                delta = new
                {
                    recall_delta = 0.2,
                    mrr_delta = 0.0,
                    ndcg_delta = 0.403,
                    newly_retrieved_relevant_docs = new[] { "06", "05" }
                }
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    internal static string BuildUnavailableDiagnosticsJson(string reason)
    {
        var payload = new
        {
            retrieval_diagnostics = new
            {
                status = "unavailable",
                reason
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    internal static string BuildNotComputableDiagnosticsJson(string reason)
    {
        var payload = new
        {
            retrieval_diagnostics = new
            {
                status = "not_computable",
                reason
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
