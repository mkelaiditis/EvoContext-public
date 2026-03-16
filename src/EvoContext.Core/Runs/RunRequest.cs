namespace EvoContext.Core.Runs;

public enum RunMode
{
    Run1SimilarityOnly,
    Run1AnswerGeneration,
    Run2FeedbackExpanded,
    Run3AnswerGeneration
}

public sealed record RunRequest(
    string ScenarioId,
    string TaskText,
    RunMode RunMode);
