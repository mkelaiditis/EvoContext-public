using EvoContext.Core.Evidence;
using EvoContext.Core.Logging;
using Serilog;
using CoreContextPack = EvoContext.Core.Context.ContextPack;

namespace EvoContext.Infrastructure.Services;

public sealed record AnswerGenerationResult(
    string Answer,
    string PromptTemplateVersion,
    AnswerFormatValidationResult Validation,
    string EvidenceBlock = "");

public sealed class AnswerGenerationService
{
    private readonly Phase3PromptBuilder _promptBuilder;
    private readonly IAnswerGenerator _generator;
    private readonly AnswerFormatValidator _validator;
    private readonly ILogger _logger;

    public AnswerGenerationService(
        Phase3PromptBuilder promptBuilder,
        IAnswerGenerator generator,
        AnswerFormatValidator validator,
        ILogger logger)
    {
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AnswerGenerationResult> GenerateAsync(
        string question,
        CoreContextPack contextPack,
        string? scenarioId = null,
        IReadOnlyList<DetectedEvidenceItem>? detectedEvidence = null,
        CancellationToken cancellationToken = default)
    {
        if (contextPack is null)
        {
            throw new ArgumentNullException(nameof(contextPack));
        }

        var prompt = _promptBuilder.Build(question, contextPack.Text, scenarioId, detectedEvidence);
        var answer = await _generator
            .GenerateAnswerAsync(prompt.SystemPrompt, prompt.UserPrompt, cancellationToken)
            .ConfigureAwait(false);
        var validation = _validator.Validate(answer);

        if (!validation.WordCountWithinRange)
        {
            _logger.Warning("Answer word count out of range: {WordCount}", validation.WordCount);
        }

        _logger
            .WithProperties(
                ("scenario_id", scenarioId ?? string.Empty),
                ("answer_length", answer.Length),
                ("prompt_template_version", prompt.TemplateVersion),
                ("word_count", validation.WordCount),
                ("word_count_within_range", validation.WordCountWithinRange))
            .Debug("Answer generation completed");

        return new AnswerGenerationResult(answer, prompt.TemplateVersion, validation, prompt.EvidenceBlock);
    }
}
