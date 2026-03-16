<!--
Source of truth: src/EvoContext.Cli/Program.cs (BuildConfiguration) and usage sites in CliCommandExecutor.cs and Run5ServiceFactory.cs
If new environment variables are added, update this document accordingly.
-->

# Configuration

## Load order

CLI configuration is loaded in this order:

1. `appsettings.json` (optional)
2. User secrets (`AddUserSecrets<CliSecrets>(optional: true)`)
3. Environment variables (`AddEnvironmentVariables()`)

Later sources override earlier sources.

## Environment variables and secrets

| Name | Required | Description |
| --- | --- | --- |
| `OPENAI_API_KEY` | Yes | OpenAI API key used for embeddings and generation. |
| `QDRANT_URL` | Yes | Qdrant instance URL (for example `http://localhost:6333`). |
| `QDRANT_API_KEY` | No | Qdrant API key. Omit for local instances without authentication. |

## Setting secrets with dotnet user-secrets

The CLI project uses `CliSecrets` in `EvoContext.Cli` as the user-secrets marker class.

Set secrets for the CLI project with:

```bash
dotnet user-secrets set OPENAI_API_KEY "<your-openai-api-key>" --project src/EvoContext.Cli/EvoContext.Cli.csproj
dotnet user-secrets set QDRANT_URL "http://localhost:6333" --project src/EvoContext.Cli/EvoContext.Cli.csproj
dotnet user-secrets set QDRANT_API_KEY "<your-qdrant-api-key>" --project src/EvoContext.Cli/EvoContext.Cli.csproj
```

## appsettings.json note

`appsettings.json` in the CLI project is used for Serilog logging configuration and should not be used to store secrets.
