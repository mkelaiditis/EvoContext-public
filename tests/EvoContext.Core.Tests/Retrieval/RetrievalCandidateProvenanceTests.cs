using EvoContext.Core.Retrieval;

namespace EvoContext.Core.Tests.Retrieval;

public sealed class RetrievalCandidateProvenanceTests
{
    [Fact]
    public void Constructor_RemainsBackwardCompatible_WhenOptionalFieldsAreOmitted()
    {
        var candidate = new RetrievalCandidate(
            "q1",
            1,
            0.9f,
            0.9f,
            "01",
            "01_0",
            0,
            "chunk text");

        Assert.Null(candidate.DocumentTitle);
        Assert.Null(candidate.Section);
        Assert.Null(candidate.QueryText);
    }

    [Fact]
    public void Constructor_AllowsOptionalProvenanceFields()
    {
        var candidate = new RetrievalCandidate(
            "q2",
            2,
            0.8f,
            0.8f,
            "02",
            "02_1",
            1,
            "chunk text",
            "Runbook 502",
            "Restart Sequence",
            "service returns 502");

        Assert.Equal("Runbook 502", candidate.DocumentTitle);
        Assert.Equal("Restart Sequence", candidate.Section);
        Assert.Equal("service returns 502", candidate.QueryText);
    }
}
