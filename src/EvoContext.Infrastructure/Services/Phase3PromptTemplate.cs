namespace EvoContext.Infrastructure.Services;

public static class Phase3PromptTemplate
{
    public const string TemplateVersion = "v4";

    public const string SystemInstructions =
        "You answer questions strictly using the provided policy documents.\n" +
        "Do not invent policy conditions not present in the context.\n" +
        "If a rule is not present in the context, do not include it in the answer.\n" +
        "The answer must follow the required output structure exactly.\n" +
        "Treat 'twelve-month subscription plan', 'twelve-month contract', and 'twelve-month service agreement' as equivalent to 'annual subscription' in this policy set.";

    public const string AnswerFormatInstructions =
        "Coverage requirement:\n" +
        "If the context includes a specific early termination and prorated reimbursement mechanism — " +
        "such as a clause about customers who cancel a twelve-month commitment before completion " +
        "and may receive prorated reimbursement for unused service value — " +
        "that mechanism must be stated explicitly in section E below. " +
        "Do not omit it and do not replace it with vague language such as \"depends on service terms\". " +
        "The prorated reimbursement mechanism for formal early termination requests is a separate exception process, " +
        "not a contradiction of the general non-refundable rule — both apply in different circumstances and must be reported independently.\n\n" +
        "A. Summary\n" +
        "Two to four sentences summarizing the refund policy for annual subscriptions.\n\n" +
        "B. Eligibility Rules\n" +
        "Bullet list describing refund eligibility conditions.\n\n" +
        "C. Exceptions\n" +
        "Bullet list describing exceptions such as billing errors or special cases.\n\n" +
        "D. Timeline and Process\n" +
        "Bullet list describing:\n" +
        "- refund processing timeline\n" +
        "- cancellation procedure\n\n" +
        "E. Annual Fixed-Term Treatment\n" +
        "Search the provided context for any of the following anchor phrases or concepts: " +
        "'prorated reimbursement', 'unused service value', 'remaining contract period', 'early termination', or 'calculated on a monthly basis'. " +
        "If any of these appear in the context for a twelve-month plan, twelve-month contract, twelve-month service agreement, or annual subscription, " +
        "you must report the clause in this section as a conditional early-termination reimbursement rule. " +
        "Do not omit this section and do not contradict the clause when such evidence is present. " +
        "Only omit this section if none of these anchors appear anywhere in the provided context. " +
        "When both a general non-refundable rule and an early-termination reimbursement clause are present, report both. " +
        "Treat the reimbursement clause as a conditional exception path, not as a contradiction.";
}
