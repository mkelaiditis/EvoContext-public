using System.Text.Json;
using EvoContext.Infrastructure.Models;

namespace EvoContext.Infrastructure.Services;

public sealed class RelevanceProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _basePath;

    public RelevanceProfileLoader(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            throw new ArgumentException("Base path is required.", nameof(basePath));
        }

        _basePath = ResolveRepoRoot(basePath);
    }

    public RelevanceProfile Load(string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("Scenario id is required.", nameof(scenarioId));
        }

        var profilePath = Path.Combine(_basePath, "data", "scenarios", scenarioId, "relevance_profile.json");
        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Relevance profile not found: {profilePath}");
        }

        var json = File.ReadAllText(profilePath);
        var profile = JsonSerializer.Deserialize<RelevanceProfile>(json, JsonOptions);
        if (profile is null)
        {
            throw new InvalidDataException($"Relevance profile could not be parsed: {profilePath}");
        }

        Validate(profile, profilePath);

        var highlyRelevantDocuments = profile.HighlyRelevantDocuments ?? Array.Empty<string>();

        return profile with
        {
            HighlyRelevantDocuments = highlyRelevantDocuments
        };
    }

    private static void Validate(RelevanceProfile profile, string profilePath)
    {
        if (profile.K <= 0)
        {
            throw new InvalidDataException($"Relevance profile k must be greater than zero: {profilePath}");
        }

        if (profile.RelevantDocuments is null)
        {
            throw new InvalidDataException($"Relevance profile missing relevant_documents: {profilePath}");
        }

        if (profile.LabelToDocumentMap is null)
        {
            throw new InvalidDataException($"Relevance profile missing label_to_document_map: {profilePath}");
        }

        if (profile.RelevantDocuments.Any(id => string.IsNullOrWhiteSpace(id)))
        {
            throw new InvalidDataException($"Relevance profile contains empty relevant document IDs: {profilePath}");
        }

        if (profile.LabelToDocumentMap.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
        {
            throw new InvalidDataException($"Relevance profile contains empty label or document mappings: {profilePath}");
        }

        if (profile.HighlyRelevantDocuments is not null && profile.HighlyRelevantDocuments.Any(id => string.IsNullOrWhiteSpace(id)))
        {
            throw new InvalidDataException($"Relevance profile contains empty highly relevant document IDs: {profilePath}");
        }
    }

    private static string ResolveRepoRoot(string basePath)
    {
        var current = new DirectoryInfo(basePath);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "EvoContext.slnx"))
                || Directory.Exists(Path.Combine(current.FullName, ".git"))
                || Directory.Exists(Path.Combine(current.FullName, ".specify")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found from base path.");
    }
}
