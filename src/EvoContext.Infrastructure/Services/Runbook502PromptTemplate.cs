namespace EvoContext.Infrastructure.Services;

public static class Runbook502PromptTemplate
{
    public const string TemplateVersion = "runbook502-v1";

    public const string SystemInstructions =
        "You answer operational troubleshooting questions strictly using the provided runbook documentation.\n" +
        "Only include steps that are explicitly described in the provided context.\n" +
        "Do not invent steps, make guarantees, or state that an issue will never recur.\n" +
        "Format the answer as a numbered checklist of troubleshooting steps.";

    public const string AnswerFormatInstructions =
        "Provide a numbered checklist of troubleshooting steps based on the context.\n" +
        "Cover only the steps that are supported by the provided context.\n" +
        "Each step should be a clear, actionable instruction written in plain language.";
}
