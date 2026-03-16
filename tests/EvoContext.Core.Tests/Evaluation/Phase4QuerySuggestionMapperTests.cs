using System;
using System.Collections.Generic;
using EvoContext.Core.Evaluation;

namespace EvoContext.Core.Tests.Evaluation;

public sealed class Phase4QuerySuggestionMapperTests
{
    [Fact]
    public void Map_DedupesAndCapsSuggestions()
    {
        var mapper = new Phase4QuerySuggestionMapper();
        var missingLabels = new List<string>
        {
            Phase4RuleTables.MissingCoolingOffWindow,
            Phase4RuleTables.MissingProcessingTimeline,
            Phase4RuleTables.MissingCoolingOffWindow,
            Phase4RuleTables.MissingAnnualProrationRule,
            Phase4RuleTables.MissingBillingErrorException,
            Phase4RuleTables.MissingCancellationProcedure
        };

        var suggestions = mapper.Map(missingLabels);

        Assert.Equal(6, suggestions.Count);
        Assert.Collection(
            suggestions,
            first => Assert.Equal("14-day cooling-off refund window", first),
            second => Assert.Equal("cooling-off period refund eligibility", second),
            third => Assert.Equal("refund processing 5-10 business days", third),
            fourth => Assert.Equal("payment method refund timeline", fourth),
            fifth => Assert.Equal("early termination prorated reimbursement", fifth),
            sixth => Assert.Equal("service commitment contract year unused service value", sixth));
    }

    [Fact]
    public void Map_ReturnsEmptyForEmptyInput()
    {
        var mapper = new Phase4QuerySuggestionMapper();

        var suggestions = mapper.Map(new List<string>());

        Assert.Empty(suggestions);
    }

    [Fact]
    public void Map_ReturnsEmptyForUnknownLabels()
    {
        var mapper = new Phase4QuerySuggestionMapper();

        var suggestions = mapper.Map(new List<string> { "UNKNOWN_LABEL" });

        Assert.Empty(suggestions);
    }

    [Fact]
    public void Map_ThrowsForNullInput()
    {
        var mapper = new Phase4QuerySuggestionMapper();

        Assert.Throws<ArgumentNullException>(() => mapper.Map(null!));
    }
}
