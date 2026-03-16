using EvoContext.Core.Evaluation;
using EvoContext.Core.Evidence;
using EvoContext.Core.Retrieval;

namespace EvoContext.Core.Tests.Evidence;

public sealed class DetectedEvidenceExtractorTests
{
    [Fact]
    public void Extract_SkipsMarkdownHeadingMatch_WhenProseOccurrenceExists()
    {
        var extractor = CreateExtractor();
        var selectedChunks = new[]
        {
            new RetrievalCandidate(
                "q",
                1,
                0.95f,
                0.95f,
                "06",
                "06_0",
                0,
                "# Service Commitment and Early Termination\n\n## Prorated reimbursement\n\nWhen an early termination request for a twelve-month contract is approved, the unused service value for the remaining contract period is eligible for prorated reimbursement. The prorated amount is calculated based on unused months within the service commitment term and the contract year termination date.")
        };

        var result = extractor.Extract(selectedChunks);

        var evidenceItem = Assert.Single(result);
        Assert.Equal("06", evidenceItem.DocumentId);
        Assert.Equal("F2", evidenceItem.FactId);
        Assert.Equal(Phase4RuleTables.PresentAnnualProrationRule, evidenceItem.FactLabel);
        Assert.Equal("prorated reimbursement", evidenceItem.MatchedAnchor);
        Assert.Equal(
            "When an early termination request for a twelve-month contract is approved, the unused service value for the remaining contract period is eligible for prorated reimbursement.",
            evidenceItem.ExtractedSnippet);
    }

    [Fact]
    public void Extract_DoesNotEmitEvidence_WhenOnlyHeadingMatchesExist()
    {
        var extractor = CreateExtractor();
        var selectedChunks = new[]
        {
            new RetrievalCandidate(
                "q",
                1,
                0.95f,
                0.95f,
                "06",
                "06_0",
                0,
                "# Service Commitment and Early Termination\n\n## Prorated reimbursement\n\n## Review process")
        };

        var result = extractor.Extract(selectedChunks);

        Assert.Empty(result);
    }

    private static DetectedEvidenceExtractor CreateExtractor()
    {
        return new DetectedEvidenceExtractor(
            new[]
            {
                new FactRule(
                    "F2",
                    Phase4RuleTables.MissingAnnualProrationRule,
                    Array.Empty<string>(),
                    new[] { "prorated reimbursement" },
                    RequiresDualAnswerMatch: false)
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["F2"] = Phase4RuleTables.PresentAnnualProrationRule
            });
    }
}