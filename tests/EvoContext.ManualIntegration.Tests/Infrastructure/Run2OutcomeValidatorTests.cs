namespace EvoContext.ManualIntegration.Tests.Infrastructure;

public sealed class Run2OutcomeValidatorTests
{
    [Fact]
    public void Validate_TreatsDoesNotMentionAsAbsentProrationInRun1Answer()
    {
        var validator = new Run2OutcomeValidator();
        var artifact = new RunVerificationArtifact(
            "artifacts/traces/policy_refund_v1/run.json",
            "policy_refund_v1_20990101T000000Z_abcd",
            60,
            85,
            25,
            "Customers may receive prorated reimbursement for unused service value.",
            new[] { "06" });

        var result = validator.Validate(
            artifact,
            "The provided context does not mention prorated reimbursement for unused months.");

        Assert.True(result.Run1ProrationAbsent);
    }
}