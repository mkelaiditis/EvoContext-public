namespace EvoContext.Core.Evaluation;

public sealed record FactRule
{
    public FactRule(
        string FactId,
        string MissingLabel,
        IReadOnlyList<string> AnswerPatterns,
        IReadOnlyList<string> ContextAnchors,
        bool RequiresDualAnswerMatch,
        IReadOnlyList<string>? SecondaryAnswerPatterns = null,
        IReadOnlyList<string>? NegationGuardPatterns = null)
    {
        this.FactId = FactId;
        this.MissingLabel = MissingLabel;
        this.AnswerPatterns = AnswerPatterns;
        this.ContextAnchors = ContextAnchors;
        this.RequiresDualAnswerMatch = RequiresDualAnswerMatch;
        this.SecondaryAnswerPatterns = SecondaryAnswerPatterns;
        this.NegationGuardPatterns = NegationGuardPatterns ?? Array.Empty<string>();
    }

    public string FactId { get; init; }

    public string MissingLabel { get; init; }

    public IReadOnlyList<string> AnswerPatterns { get; init; }

    public IReadOnlyList<string> ContextAnchors { get; init; }

    public bool RequiresDualAnswerMatch { get; init; }

    public IReadOnlyList<string>? SecondaryAnswerPatterns { get; init; }

    public IReadOnlyList<string> NegationGuardPatterns { get; init; }
}

public sealed record HallucinationRule(
    string Flag,
    IReadOnlyList<string> Patterns);

public static class Phase4RuleTables
{
    public const string PresentCoolingOffWindow = "PRESENT_COOLING_OFF_WINDOW";
    public const string PresentAnnualProrationRule = "PRESENT_ANNUAL_PRORATION_RULE";
    public const string PresentBillingErrorException = "PRESENT_BILLING_ERROR_EXCEPTION";
    public const string PresentProcessingTimeline = "PRESENT_PROCESSING_TIMELINE";
    public const string PresentCancellationProcedure = "PRESENT_CANCELLATION_PROCEDURE";

    public const string MissingCoolingOffWindow = "MISSING_COOLING_OFF_WINDOW";
    public const string MissingAnnualProrationRule = "MISSING_ANNUAL_PRORATION_RULE";
    public const string MissingBillingErrorException = "MISSING_BILLING_ERROR_EXCEPTION";
    public const string MissingProcessingTimeline = "MISSING_PROCESSING_TIMELINE";
    public const string MissingCancellationProcedure = "MISSING_CANCELLATION_PROCEDURE";

    public const string HallucinatedTimeWindow = "HALLUCINATED_TIME_WINDOW";
    public const string HallucinatedFeesOrPenalties = "HALLUCINATED_FEES_OR_PENALTIES";
    public const string HallucinatedRefundGuarantee = "HALLUCINATED_REFUND_GUARANTEE";
    public const string HallucinatedExtraRequirements = "HALLUCINATED_EXTRA_REQUIREMENTS";

    public static readonly IReadOnlyList<FactRule> FactRules = new List<FactRule>
    {
        new(
            "F1",
            MissingCoolingOffWindow,
            new[]
            {
                "14 day",
                "fourteen day",
                "cooling-off",
                "within 14 days"
            },
            new[]
            {
                "14-day cooling-off period",
                "refund within 14 days",
                "cooling-off window"
            },
            RequiresDualAnswerMatch: false),
        new(
            "F2",
            MissingAnnualProrationRule,
            new[]
            {
                "eligible for prorated reimbursement",
                "prorated reimbursement",
                "prorated refund",
                "prorated amount",
                "unused service value"
            },
            new[]
            {
                "prorated reimbursement",
                "unused service value",
                "service commitment term",
                "contract year termination"
            },
            RequiresDualAnswerMatch: true,
            SecondaryAnswerPatterns: new[]
            {
                "refund",
                "reimburse"
            },
            NegationGuardPatterns: new[]
            {
                "no prorat",
                "not eligible for prorat",
                "no early termination",
                "no clause",
                "not specified",
                "section omitted",
                "does not mention",
                "does not include",
                "is omitted"
            }),
        new(
            "F3",
            MissingBillingErrorException,
            new[]
            {
                "duplicate charge",
                "billing error",
                "incorrect charge",
                "processing error",
                "mistaken charge"
            },
            new[]
            {
                "duplicate charges",
                "billing errors",
                "incorrect charge adjustment",
                "refund due to error"
            },
            RequiresDualAnswerMatch: false),
        new(
            "F4",
            MissingProcessingTimeline,
            new[]
            {
                "5-10 business days",
                "five to ten business days",
                "within 5 business days",
                "up to 10 business days"
            },
            new[]
            {
                "processed within 5-10 business days",
                "refund timeline",
                "payment method processing time"
            },
            RequiresDualAnswerMatch: false),
        new(
            "F5",
            MissingCancellationProcedure,
            new[]
            {
                "cancel via portal",
                "contact support",
                "submit request"
            },
            new[]
            {
                "account portal",
                "contact support",
                "account identifier",
                "cancellation request"
            },
            RequiresDualAnswerMatch: true,
            SecondaryAnswerPatterns: new[]
            {
                "account id",
                "registered email",
                "account identifier"
            })
    };

    public static readonly IReadOnlyList<HallucinationRule> HallucinationRules = new List<HallucinationRule>
    {
        new(
            HallucinatedTimeWindow,
            new[]
            {
                "30-day refund period",
                "60-day cancellation window"
            }),
        new(
            HallucinatedFeesOrPenalties,
            new[]
            {
                "cancellation fee",
                "termination penalty",
                "administrative charge"
            }),
        new(
            HallucinatedRefundGuarantee,
            new[]
            {
                "always refundable",
                "guaranteed refund",
                "full refund guaranteed"
            }),
        new(
            HallucinatedExtraRequirements,
            new[]
            {
                "signed form required",
                "phone call required",
                "identity verification required"
            })
    };

    // No contradiction patterns are defined in current locked docs; placeholder keeps cap logic deterministic.
    public static readonly IReadOnlyList<string> ContradictionPatterns = Array.Empty<string>();

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> QuerySuggestions
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [MissingCoolingOffWindow] = new[]
            {
                "14-day cooling-off refund window",
                "cooling-off period refund eligibility"
            },
            [MissingAnnualProrationRule] = new[]
            {
                "early termination prorated reimbursement",
                "service commitment contract year unused service value"
            },
            [MissingBillingErrorException] = new[]
            {
                "duplicate charge billing error refund",
                "incorrect charge adjustment refund"
            },
            [MissingProcessingTimeline] = new[]
            {
                "refund processing 5-10 business days",
                "payment method refund timeline"
            },
            [MissingCancellationProcedure] = new[]
            {
                "cancel via portal contact support account ID",
                // Display strings keep original casing; normalization is applied only for matching.
                "cancellation process required account identifier"
            }
        };

    public static readonly IReadOnlyDictionary<string, string> PresentLabelByFactId
        = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["F1"] = PresentCoolingOffWindow,
            ["F2"] = PresentAnnualProrationRule,
            ["F3"] = PresentBillingErrorException,
            ["F4"] = PresentProcessingTimeline,
            ["F5"] = PresentCancellationProcedure
        };
}
