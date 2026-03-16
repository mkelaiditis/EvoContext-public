using EvoContext.Core.Evidence;
using System.Text;

namespace EvoContext.Infrastructure.Services;

public sealed record Phase3Prompt(
    string SystemPrompt,
    string UserPrompt,
    string TemplateVersion,
    string EvidenceBlock = "");

public sealed class Phase3PromptBuilder
{
    public Phase3Prompt Build(
        string question,
        string packedContext,
        string? scenarioId = null,
        IReadOnlyList<DetectedEvidenceItem>? detectedEvidence = null)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question must be non-empty.", nameof(question));
        }

        // Empty context is allowed to represent "no relevant details" in the pack.
        if (packedContext is null)
        {
            throw new ArgumentNullException(nameof(packedContext));
        }

        if (string.Equals(scenarioId, "runbook_502_v1", StringComparison.Ordinal))
        {
            var runbookUserPrompt =
                "Context:\n" + packedContext +
                "\n\nQuestion:\n" + question +
                "\n\nAnswer Instructions:\n" + Runbook502PromptTemplate.AnswerFormatInstructions;

            return new Phase3Prompt(
                Runbook502PromptTemplate.SystemInstructions,
                runbookUserPrompt,
                Runbook502PromptTemplate.TemplateVersion);
        }

        var evidenceBlock = BuildEvidenceBlock(detectedEvidence);
        var userPrompt =
            "Context:\n" + packedContext +
            evidenceBlock +
            "\n\nQuestion:\n" + question +
            "\n\nAnswer Instructions:\n" + Phase3PromptTemplate.AnswerFormatInstructions;

        return new Phase3Prompt(
            Phase3PromptTemplate.SystemInstructions,
            userPrompt,
            Phase3PromptTemplate.TemplateVersion,
            evidenceBlock);
    }

    private static string BuildEvidenceBlock(IReadOnlyList<DetectedEvidenceItem>? items)
    {
        if (items is null || items.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append("\n\nDetected evidence from retrieved context:");
        foreach (var item in items)
        {
            sb.Append($"\n- Document {item.DocumentId} [{item.FactLabel}]: \"{item.ExtractedSnippet}\"");
        }

        sb.Append("\nYou must incorporate all detected evidence items above into your answer. Do not contradict or omit any detected evidence.");
        return sb.ToString();
    }
}
