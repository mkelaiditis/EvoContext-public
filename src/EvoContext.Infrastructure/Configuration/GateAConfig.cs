namespace EvoContext.Infrastructure.Configuration;

public sealed class GateAConfig
{
    public string Host { get; }
    public int Port { get; }
    public bool UseHttps { get; }
    public string? ApiKey { get; }
    public string CollectionName { get; }

    private GateAConfig(string host, int port, bool useHttps, string? apiKey, string collectionName)
    {
        Host = host;
        Port = port;
        UseHttps = useHttps;
        ApiKey = apiKey;
        CollectionName = collectionName;
    }

    public static GateAConfig LoadFromEnvironment()
    {
        return Load(
            Environment.GetEnvironmentVariable("QDRANT_URL"),
            Environment.GetEnvironmentVariable("QDRANT_API_KEY"),
            Environment.GetEnvironmentVariable("QDRANT_COLLECTION"));
    }

    public static GateAConfig Load(string? qdrantUrl, string? apiKey, string? collectionName)
    {
        var resolvedUrl = string.IsNullOrWhiteSpace(qdrantUrl) ? "http://localhost:6333" : qdrantUrl.Trim();
        if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid Qdrant URL: {resolvedUrl}", nameof(qdrantUrl));
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException($"Qdrant URL must be http or https: {resolvedUrl}", nameof(qdrantUrl));
        }

        var useHttps = uri.Scheme == Uri.UriSchemeHttps;
        var port = uri.IsDefaultPort ? (useHttps ? 6334 : 6333) : uri.Port;
        var host = uri.Host;
        var resolvedCollectionName = string.IsNullOrWhiteSpace(collectionName)
            ? "evocontext-gate-a"
            : collectionName.Trim();
        var resolvedApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();

        return new GateAConfig(host, port, useHttps, resolvedApiKey, resolvedCollectionName);
    }
}
