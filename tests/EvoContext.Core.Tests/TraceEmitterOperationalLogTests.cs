using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests;

public sealed class TraceEmitterOperationalLogTests
{
    [Fact]
    public async Task EmitAsync_RedactsAnswerAndScoreFields_FromOperationalTraceLogs()
    {
        using var temp = new TempDirectory();
        var runId = "policy_refund_v1_20260310T120000Z_abcd";
        var path = Path.Combine(temp.Path, runId + ".jsonl");

        using (var emitter = new TraceEmitter(temp.Path))
        {
            await emitter.EmitAsync(new TraceEvent(
                TraceEventType.GenerationCompleted,
                runId,
                "policy_refund_v1",
                1,
                new Dictionary<string, object?>
                {
                    ["raw_model_output"] = "sensitive answer text",
                    ["prompt_question"] = "question"
                }),
                TestContext.Current.CancellationToken);

            await emitter.EmitAsync(new TraceEvent(
                TraceEventType.EvaluationCompleted,
                runId,
                "policy_refund_v1",
                2,
                new Dictionary<string, object?>
                {
                    ["score_total"] = 60,
                    ["score_run1"] = 60,
                    ["score_run2"] = 70,
                    ["score_delta"] = 10,
                    ["missing_items"] = new[] { "MISSING_COOLING_OFF_WINDOW" }
                }),
                TestContext.Current.CancellationToken);
        }

        var content = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("raw_model_output", content, StringComparison.Ordinal);
        Assert.DoesNotContain("score_total", content, StringComparison.Ordinal);
        Assert.DoesNotContain("score_run1", content, StringComparison.Ordinal);
        Assert.DoesNotContain("score_run2", content, StringComparison.Ordinal);
        Assert.DoesNotContain("score_delta", content, StringComparison.Ordinal);

        Assert.Contains("prompt_question", content, StringComparison.Ordinal);
        Assert.Contains("missing_items", content, StringComparison.Ordinal);
    }
}
