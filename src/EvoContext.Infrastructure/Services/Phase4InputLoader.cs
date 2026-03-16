using System.Text.Json;
using EvoContext.Core.Evaluation;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Infrastructure.Services;

public sealed class Phase4InputLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<EvaluationInput> LoadAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path is required.", nameof(inputPath));
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Input JSON file not found.", inputPath);
        }

        var json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        var model = JsonSerializer.Deserialize<Phase4EvaluationInputModel>(json, SerializerOptions);

        if (model is null)
        {
            throw new InvalidOperationException("Input JSON is invalid or empty.");
        }

        if (model.SelectedChunks is null || model.SelectedChunks.Count == 0)
        {
            throw new InvalidOperationException("Input must include at least one selected chunk.");
        }

        var selectedChunks = model.SelectedChunks
            .Select(chunk => new SelectedChunk(
                chunk.DocumentId,
                chunk.ChunkId,
                chunk.ChunkIndex,
                chunk.ChunkText))
            .ToList();

        return new EvaluationInput(
            model.RunId,
            model.ScenarioId,
            model.AnswerText,
            selectedChunks);
    }
}
