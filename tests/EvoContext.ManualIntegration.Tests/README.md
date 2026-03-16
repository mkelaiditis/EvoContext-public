# EvoContext.ManualIntegration.Tests

Manual-only xUnit project for the Phase 11 full-pipeline verification scenario.

## Scope

This project will host the operator-faithful verification flow for `policy_refund_v1` by executing the real CLI and validating structured artifacts.

## Prerequisites

- .NET 10 SDK
- Access to the live OpenAI and Qdrant resources used by the CLI
- `OPENAI_API_KEY`
- `QDRANT_URL`
- `QDRANT_API_KEY` when the Qdrant deployment requires authentication

## Environment Variable Setup

PowerShell:

```powershell
$env:OPENAI_API_KEY = "<your-openai-api-key>"
$env:QDRANT_URL = "http://localhost:6333"
$env:QDRANT_API_KEY = "<your-qdrant-api-key>"
```

The manual integration project launches `src/EvoContext.Cli`, so the same values may also be configured with CLI user secrets:

```powershell
dotnet user-secrets set OPENAI_API_KEY "<your-openai-api-key>" --project src/EvoContext.Cli/EvoContext.Cli.csproj
dotnet user-secrets set QDRANT_URL "http://localhost:6333" --project src/EvoContext.Cli/EvoContext.Cli.csproj
dotnet user-secrets set QDRANT_API_KEY "<your-qdrant-api-key>" --project src/EvoContext.Cli/EvoContext.Cli.csproj
```

The manual integration project also honors its own user-secrets store for these same keys and forwards the resolved values to the spawned CLI process. That allows this project to be configured directly with:

```powershell
dotnet user-secrets init --project tests/EvoContext.ManualIntegration.Tests
dotnet user-secrets set "OPENAI_API_KEY" "sk-proj-......." --project tests/EvoContext.ManualIntegration.Tests
dotnet user-secrets set "QDRANT_API_KEY" "eyJhbGci........." --project tests/EvoContext.ManualIntegration.Tests
```

`QDRANT_URL` is still required for the live run. Set it in the same user-secrets store when the manual test should target a specific Qdrant endpoint:

```powershell
dotnet user-secrets set "QDRANT_URL" "http://localhost:6333" --project tests/EvoContext.ManualIntegration.Tests
```

## Configuration

The manual integration test project does not introduce a parallel configuration system. It launches `src/EvoContext.Cli`, so the CLI configuration precedence is authoritative.

`EvoContext.Cli` currently loads configuration in this order:

1. `appsettings.json` from the CLI output directory, if present
2. `appsettings.{environment}.json`, if present, where `{environment}` comes from `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT`
3. User secrets for `EvoContext.Cli`
4. Environment variables

Later sources override earlier sources. Secrets should be supplied through user secrets or environment variables rather than appsettings files.

## Manual Run Command

```powershell
dotnet test tests/EvoContext.ManualIntegration.Tests/EvoContext.ManualIntegration.Tests.csproj --filter Category=ManualIntegration
```

## What The Verification Does

- Runs `embed --scenario policy_refund_v1`
- Runs `run --scenario policy_refund_v1 --query "What is the refund policy for annual subscriptions?" --mode run2`
- Enforces a 3-minute timeout per CLI step and a 5-minute total timeout
- Validates structured artifacts under `artifacts/traces/policy_refund_v1/`

## Output

The verification reads structured trace artifacts plus additive Run 1 verification evidence and reports:

- Combined preparation result for `embed`
- Run 1 score, Run 2 score, and score delta
- Run 1 selected chunks and Run 2 selected chunks
- Exact field paths used for validation
- Final pass or fail result

On failure, the diagnostic output also includes the Run 1 answer, the Run 2 answer, and the failed validation conditions.