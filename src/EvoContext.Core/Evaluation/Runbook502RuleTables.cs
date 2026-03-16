namespace EvoContext.Core.Evaluation;

public sealed record Runbook502StepRule(
    string StepLabel,
    IReadOnlyList<string> DetectionPatterns,
    IReadOnlyList<string> ContextAnchors);

public sealed record Runbook502HallucinationRule(
    string Flag,
    string Pattern,
    string? ContextSuppressTerm = null);

public static class Runbook502RuleTables
{
    public const string StepCheckUpstreamHealth = "STEP_CHECK_UPSTREAM_HEALTH";
    public const string StepInspectLogs = "STEP_INSPECT_LOGS";
    public const string StepCheckDeployment = "STEP_CHECK_DEPLOYMENT";
    public const string StepRollbackDeployment = "STEP_ROLLBACK_DEPLOYMENT";

    public static readonly IReadOnlyList<string> RequiredStepOrder = new[]
    {
        StepCheckUpstreamHealth,
        StepInspectLogs,
        StepCheckDeployment,
        StepRollbackDeployment
    };

    public static readonly IReadOnlyList<Runbook502StepRule> StepRules = new[]
    {
        new Runbook502StepRule(
            StepCheckUpstreamHealth,
            new[]
            {
                "check.*upstream",
                "upstream.*health",
                "health.*depend",
                "depend.*health",
                "check.*dependency",
                "verify.*dependent"
            },
            new[]
            {
                "verify that all dependent services are operating correctly",
                "check the health status of each dependency"
            }),
        new Runbook502StepRule(
            StepInspectLogs,
            new[]
            {
                "inspect.*log",
                "check.*log",
                "review.*log",
                "examine.*log"
            },
            new[]
            {
                "locating the most recent log entries",
                "begin by locating"
            }),
        new Runbook502StepRule(
            StepCheckDeployment,
            new[]
            {
                "recent.*deploy",
                "inspect.*deploy",
                "deploy.*histor",
                "check.*deploy",
                "deployment.*recent",
                "deploy.*check"
            },
            new[]
            {
                "inspect the deployment history",
                "recent deployment coincides"
            }),
        new Runbook502StepRule(
            StepRollbackDeployment,
            new[]
            {
                "roll.*back",
                "rollback",
                "revert.*deploy"
            },
            new[]
            {
                "rollback to the previous stable version",
                "initiate a rollback"
            })
    };

    public static readonly IReadOnlyDictionary<string, string> QuerySuggestionByStep =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [StepCheckUpstreamHealth] = "What should I check if a dependent service is unavailable?",
            [StepInspectLogs] = "How do I inspect service logs for 502 errors?",
            [StepCheckDeployment] = "How do I check if a recent deployment caused a 502 error?",
            [StepRollbackDeployment] = "What is the deployment rollback procedure?"
        };

    public static readonly IReadOnlyList<Runbook502HallucinationRule> HallucinationRules = new[]
    {
        new Runbook502HallucinationRule("HALLUCINATION_ALWAYS_CAUSED_BY", "always caused by"),
        new Runbook502HallucinationRule("HALLUCINATION_GUARANTEED_TO_FIX", "guaranteed to fix"),
        new Runbook502HallucinationRule("HALLUCINATION_WILL_NEVER_RECUR", "will never recur"),
        new Runbook502HallucinationRule("HALLUCINATION_IMMEDIATELY_RESOLVES", "immediately resolves"),
        new Runbook502HallucinationRule("HALLUCINATION_502_TIMEOUT", "502 timeout", "502"),
        new Runbook502HallucinationRule("HALLUCINATION_GATEWAY_RESET", "gateway.*reset", "gateway"),
        new Runbook502HallucinationRule("HALLUCINATION_502_UPSTREAM_MEANING", "502 means the upstream", "502")
    };
}
