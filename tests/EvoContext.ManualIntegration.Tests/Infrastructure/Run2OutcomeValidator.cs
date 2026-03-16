using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal sealed record Run2ValidationResult(
    int ScoreRun1,
    int? ScoreRun2,
    int? ScoreDelta,
    IReadOnlyList<string> Run2SelectedChunkDocumentIds,
    string Run2Answer,
    bool ScoresPresent,
    bool ScoreDeltaOk,
    bool Run2Document06Found,
    IReadOnlyList<string> ProrationMatches,
    bool? CoolingOffSignalPresent,
    bool? Run1ProrationAbsent,
    IReadOnlyList<string> FailedConditions)
{
    public bool ProrationEvidenceDetected => ProrationMatches.Count > 0;

    public bool Passed => FailedConditions.Count == 0;
}

internal sealed class Run2OutcomeValidator
{
    private static readonly string[] AcceptedProrationPhrases =
    [
        "prorated reimbursement",
        "prorated refund",
        "unused portion of the contract year",
        "remaining full months",
        "unused service value"
    ];

    private static readonly string[] ProrationNegationGuards =
    [
        "no prorat",
        "not eligible for prorat",
        "no early termination",
        "no clause",
        "not specified",
        "section omitted",
        "does not mention",
        "does not include",
        "is omitted"
    ];

    private static readonly string[] CoolingOffPhrases =
    [
        "cooling off",
        "cooling-off",
        "14 day cooling off",
        "14-day cooling-off",
        "14 day cooling off period",
        "14-day cooling-off period"
    ];

    public Run2ValidationResult Validate(RunVerificationArtifact artifact, string? run1Answer = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var scoresPresent = artifact.ScoreRun2.HasValue;
        var scoreDeltaOk = artifact.ScoreDelta is >= 20;
        var run2Document06Found = artifact.Run2SelectedChunkDocumentIds.Any(
            static documentId => string.Equals(documentId, "06", StringComparison.OrdinalIgnoreCase));

        var normalizedRun2Answer = Normalize(artifact.Run2Answer);
        var prorationMatches = AcceptedProrationPhrases
            .Where(phrase => ContainsAffirmative(normalizedRun2Answer, Normalize(phrase)))
            .ToArray();

        bool? coolingOffSignalPresent = null;
        bool? run1ProrationAbsent = null;

        if (!string.IsNullOrWhiteSpace(run1Answer))
        {
            var normalizedRun1Answer = Normalize(run1Answer);
            coolingOffSignalPresent = CoolingOffPhrases.Any(
                phrase => normalizedRun1Answer.Contains(Normalize(phrase), StringComparison.Ordinal));
            run1ProrationAbsent = !AcceptedProrationPhrases.Any(
                phrase => ContainsAffirmative(normalizedRun1Answer, Normalize(phrase)));
        }

        var failedConditions = new List<string>();
        if (!scoresPresent)
        {
            failedConditions.Add("Run 2 score is missing.");
        }

        if (!scoreDeltaOk)
        {
            failedConditions.Add($"Score delta must be at least 20 but was {artifact.ScoreDelta?.ToString() ?? "<missing>"}.");
        }

        if (!run2Document06Found)
        {
            failedConditions.Add("Run 2 selected chunks do not include document 06.");
        }

        if (prorationMatches.Length == 0)
        {
            failedConditions.Add("Run 2 answer does not contain accepted proration evidence.");
        }

        return new Run2ValidationResult(
            artifact.ScoreRun1,
            artifact.ScoreRun2,
            artifact.ScoreDelta,
            artifact.Run2SelectedChunkDocumentIds,
            artifact.Run2Answer,
            scoresPresent,
            scoreDeltaOk,
            run2Document06Found,
            prorationMatches,
            coolingOffSignalPresent,
            run1ProrationAbsent,
            failedConditions);
    }

    private static bool ContainsAffirmative(string normalizedText, string normalizedPhrase)
    {
        const int WindowSize = 100;
        var searchStart = 0;

        while (searchStart < normalizedText.Length)
        {
            var matchIndex = normalizedText.IndexOf(normalizedPhrase, searchStart, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                break;
            }

            var windowStart = Math.Max(0, matchIndex - WindowSize);
            var windowEnd = Math.Min(normalizedText.Length, matchIndex + normalizedPhrase.Length + WindowSize);
            var window = normalizedText.Substring(windowStart, windowEnd - windowStart);

            if (!ProrationNegationGuards.Any(g => window.Contains(g, StringComparison.Ordinal)))
            {
                return true;
            }

            searchStart = matchIndex + normalizedPhrase.Length;
        }

        return false;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lowerInvariant = value.ToLowerInvariant();
        var normalizedWhitespace = Regex.Replace(lowerInvariant, "[^a-z0-9]+", " ");
        return normalizedWhitespace.Trim();
    }
}