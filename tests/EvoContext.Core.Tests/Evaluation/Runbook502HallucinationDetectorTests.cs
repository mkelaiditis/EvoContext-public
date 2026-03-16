using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Runbook502HallucinationDetectorTests
{
    [Fact]
    public void EvaluateNormalized_AppliesPenalty_WhenUnconditionalPatternIsPresent()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize("This action is guaranteed to fix the incident.");
        var context = Phase4TextNormalizer.Normalize("Runbook provides troubleshooting guidance only.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.NotEmpty(result.HallucinationFlags);
        Assert.Equal(20, result.HallucinationPenalty);
    }

    [Fact]
    public void EvaluateNormalized_SuppressesConditionalPattern_WhenContextContainsRequiredTerm()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize("The gateway reset is the root cause.");
        var context = Phase4TextNormalizer.Normalize("The gateway component is discussed in this context.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.Empty(result.HallucinationFlags);
        Assert.Equal(0, result.HallucinationPenalty);
    }

    // A4: Remaining unconditional rules

    [Fact]
    public void EvaluateNormalized_AppliesPenalty_ForAlwaysCausedByPattern()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize("This is always caused by a misconfiguration.");
        var context = Phase4TextNormalizer.Normalize("Runbook provides troubleshooting guidance only.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.Contains("HALLUCINATION_ALWAYS_CAUSED_BY", result.HallucinationFlags);
        Assert.Equal(20, result.HallucinationPenalty);
    }

    [Fact]
    public void EvaluateNormalized_AppliesPenalty_ForWillNeverRecurPattern()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize("After this fix the problem will never recur.");
        var context = Phase4TextNormalizer.Normalize("Runbook provides troubleshooting guidance only.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.Contains("HALLUCINATION_WILL_NEVER_RECUR", result.HallucinationFlags);
        Assert.Equal(20, result.HallucinationPenalty);
    }

    [Fact]
    public void EvaluateNormalized_AppliesPenalty_ForImmediatelyResolvesPattern()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize("Restarting the service immediately resolves all startup errors.");
        var context = Phase4TextNormalizer.Normalize("Runbook provides troubleshooting guidance only.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.Contains("HALLUCINATION_IMMEDIATELY_RESOLVES", result.HallucinationFlags);
        Assert.Equal(20, result.HallucinationPenalty);
    }

    // A4: Conditional rule fires when context does NOT contain the suppress term

    [Fact]
    public void EvaluateNormalized_AppliesPenalty_ForConditionalPattern_WhenContextLacksSuppressTerm()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize("This is a 502 timeout condition.");
        var context = Phase4TextNormalizer.Normalize("The service failed to start due to a missing configuration file.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.Contains("HALLUCINATION_502_TIMEOUT", result.HallucinationFlags);
        Assert.Equal(20, result.HallucinationPenalty);
    }

    // A4: Additional conditional rules — suppressed and unsuppressed

    [Fact]
    public void EvaluateNormalized_Suppresses502Timeout_WhenContextContains502()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize("This is a 502 timeout condition.");
        var context = Phase4TextNormalizer.Normalize("The 502 status code indicates a gateway error.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.DoesNotContain("HALLUCINATION_502_TIMEOUT", result.HallucinationFlags);
    }

    [Fact]
    public void EvaluateNormalized_AppliesPenalty_ForGatewayReset_WhenContextLacksSuppressTerm()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize("The gateway reset caused the failure.");
        var context = Phase4TextNormalizer.Normalize("The service failed to start due to a missing configuration file.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.Contains("HALLUCINATION_GATEWAY_RESET", result.HallucinationFlags);
        Assert.Equal(20, result.HallucinationPenalty);
    }

    [Fact]
    public void EvaluateNormalized_Suppresses502UpstreamMeaning_WhenContextContains502()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize("502 means the upstream service is down.");
        var context = Phase4TextNormalizer.Normalize("The 502 status indicates a backend connectivity issue.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.DoesNotContain("HALLUCINATION_502_UPSTREAM_MEANING", result.HallucinationFlags);
    }

    [Fact]
    public void EvaluateNormalized_AppliesPenalty_For502UpstreamMeaning_WhenContextLacks502()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize("502 means the upstream service is down.");
        var context = Phase4TextNormalizer.Normalize("The service failed to start due to a missing configuration file.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.Contains("HALLUCINATION_502_UPSTREAM_MEANING", result.HallucinationFlags);
        Assert.Equal(20, result.HallucinationPenalty);
    }

    // A4: Penalty cap at 40 when multiple rules fire

    [Fact]
    public void EvaluateNormalized_CapsPenaltyAt40_WhenMultipleRulesFire()
    {
        var detector = new Runbook502HallucinationDetector();
        var answer = Phase4TextNormalizer.Normalize(string.Join(" ",
            "This is always caused by a misconfiguration.",
            "It is guaranteed to fix the issue.",
            "The problem will never recur.",
            "Restarting immediately resolves everything."));
        var context = Phase4TextNormalizer.Normalize("Runbook provides troubleshooting guidance only.");

        var result = detector.EvaluateNormalized(answer, context);

        Assert.True(result.HallucinationFlags.Count >= 3);
        Assert.Equal(40, result.HallucinationPenalty);
    }
}
