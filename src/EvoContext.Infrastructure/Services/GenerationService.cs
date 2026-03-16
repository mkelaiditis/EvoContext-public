using System;
using EvoContext.Core.Config;
using EvoContext.Core.Logging;
using OpenAI;
using OpenAI.Chat;
using Serilog;

namespace EvoContext.Infrastructure.Services;

public sealed class GenerationService : IAnswerGenerator
{
    private readonly ChatClient _client;
    private readonly CoreConfigSnapshot _config;
    private readonly ILogger _logger;

    public GenerationService(CoreConfigSnapshot config, string? apiKey = null, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = (logger ?? StructuredLogging.NullLogger).ForContext<GenerationService>();

        apiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY is required for generation.");
        }

        _client = new ChatClient(_config.GenerationModel, apiKey);
    }

    public async Task<string> GenerateAnswerAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            throw new ArgumentException("System prompt must be non-empty.", nameof(systemPrompt));
        }

        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ArgumentException("User prompt must be non-empty.", nameof(userPrompt));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = (float)_config.Temperature,
            TopP = (float)_config.TopP,
            MaxOutputTokenCount = _config.MaxTokens
        };

        ChatCompletion completion = await _client
            .CompleteChatAsync(messages, options, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var answer = completion.Content.Count > 0
            ? completion.Content[0].Text
            : string.Empty;

        _logger
            .WithProperties(
                ("generation_model", _config.GenerationModel),
                ("system_prompt_length", systemPrompt.Length),
                ("user_prompt_length", userPrompt.Length),
                ("temperature", _config.Temperature),
                ("top_p", _config.TopP),
                ("max_tokens", _config.MaxTokens),
                ("output_length", answer.Length))
            .Debug("Generation request completed");

        return answer;
    }
}
