namespace EvoContext.Infrastructure.Services;

public interface IAnswerGenerator
{
    Task<string> GenerateAnswerAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
