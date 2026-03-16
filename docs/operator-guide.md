# Operator Guide

Commands require only .NET 10 SDK — no installation step needed beyond cloning the repository.

## Two hosts

There are two runnable hosts. Use the right one for the task:

| Host | Project | Purpose |
|---|---|---|
| **CLI** | `src/EvoContext.Cli` | Dataset setup (`embed`) and operator-focused runs |
| **Demo** | `src/EvoContext.Demo` | Demo runs with rich narration and humanized labels |

CLI prints structured key/value output with raw evaluator codes (e.g. `MISSING_COOLING_OFF_WINDOW`). Demo maps those codes to plain English, shows answer summaries, and includes score improvement — this is the view intended for judges and demo audiences.

## Prerequisites

- .NET 10 SDK
- A running Qdrant instance (URL and optional API key)
- An OpenAI API key

> **Platform:** Validated on Windows. .NET 10 is cross-platform and Linux should work, but has not been tested.

> **No external services?** Skip to [Step 3 — Replay without external resources](#step-3--replay-without-external-resources). Sample trace artifacts are included in `docs/samples/` and can be replayed with no Qdrant or OpenAI connection.

## Configuration

Set the following environment variables before running either host.

**PowerShell:**

```powershell
$env:OPENAI_API_KEY = "<your-openai-api-key>"
$env:QDRANT_URL = "http://localhost:6333"
$env:QDRANT_API_KEY = "<your-qdrant-api-key>"
```

**bash / Git Bash:**

```bash
export OPENAI_API_KEY="<your-openai-api-key>"
export QDRANT_URL="http://localhost:6333"
export QDRANT_API_KEY="<your-qdrant-api-key>"
```

Both hosts pick these up automatically via the .NET configuration API. No per-project setup needed.

For full configuration behavior and precedence, see [docs/reference/configuration.md](reference/configuration.md).

## Step 0 — Dry-run document preview (optional)

`ingest` loads and chunks scenario documents without calling OpenAI or Qdrant. It prints document and chunk counts, and exits. Nothing is persisted. Use it to confirm files are present and parseable before paying for embedding calls. Skip this step if you are confident in the dataset.

**Policy refund scenario:**

```bash
dotnet run --project src/EvoContext.Cli -- ingest --scenario policy_refund_v1
```

**Runbook scenario:**

```bash
dotnet run --project src/EvoContext.Cli -- ingest --scenario runbook_502_v1
```

## Step 1 — Dataset setup (run once per scenario, CLI)

`embed` is the only required setup step. It loads documents, generates chunks, calls OpenAI for embeddings, recreates the Qdrant collection, and stores vectors. Use the CLI host for setup.

**Policy refund scenario:**

```bash
dotnet run --project src/EvoContext.Cli -- embed --scenario policy_refund_v1
```

**Runbook scenario:**

```bash
dotnet run --project src/EvoContext.Cli -- embed --scenario runbook_502_v1
```

Gate A result for `embed --scenario policy_refund_v1`:

- Exit `0`: pass — Doc 06 (early termination policy) is not in the top 3 retrieval results.
- Exit `2`: Doc 06 appeared in top 3. Re-check chunking or collection setup.

For `runbook_502_v1`, there is no equivalent Gate A pass/fail criterion in this phase.

## Step 2 — Run a scenario

### Demo run (rich output for judges)

```bash
dotnet run --project src/EvoContext.Demo -- run --scenario policy_refund_v1 --query "What is the refund policy for annual subscriptions?" --mode run2
dotnet run --project src/EvoContext.Demo -- run --scenario runbook_502_v1 --query "The service returns 502. What do I do?" --mode run2
```

Output includes narrated answer summaries, present and missing items in plain English, and score improvement between Run 1 and Run 2.

### CLI run (structured operator output)

```bash
dotnet run --project src/EvoContext.Cli -- run --scenario policy_refund_v1 --mode run2
```

Output uses raw evaluator codes and structured key/value fields. Use this for debugging and pipeline inspection.

`--mode run1` runs a single pass with no adaptive retry. `--mode run2` enables the adaptive second pass when score is below threshold.

## Step 3 — Replay without external resources

Replay reads a stored trace artifact from disk and re-renders it. No OpenAI or Qdrant connection is required.

Sample artifacts for both scenarios are included in `docs/samples/`:

| File | Scenario | Run mode | Score Run 1 | Score Run 2 | Delta |
|---|---|---|---|---|---|
| `policy_refund_v1_20260311T151244Z_21b1.json` | Policy refund | run2 | 60 | 60 | 0 |
| `runbook_502_v1_20260316T103517Z_54ca.json` | Runbook 502 | run2 | 30 | 40 | +10 |

To replay a sample, first copy it to the expected artifacts location:

**PowerShell:**

```powershell
# Policy refund
Copy-Item docs\samples\policy_refund_v1_20260311T151244Z_21b1.json `
  -Destination artifacts\traces\policy_refund_v1\ -Force

# Runbook 502
Copy-Item docs\samples\runbook_502_v1_20260316T103517Z_54ca.json `
  -Destination artifacts\traces\runbook_502_v1\ -Force
```

**bash / Git Bash:**

```bash
# Policy refund
cp docs/samples/policy_refund_v1_20260311T151244Z_21b1.json \
  artifacts/traces/policy_refund_v1/

# Runbook 502
cp docs/samples/runbook_502_v1_20260316T103517Z_54ca.json \
  artifacts/traces/runbook_502_v1/
```

Then replay using the Demo host for rich output, or the CLI for operator output:

```bash
# Policy refund — Demo (rich)
dotnet run --project src/EvoContext.Demo -- replay --run-id policy_refund_v1_20260311T151244Z_21b1

# Runbook 502 — Demo (rich)
dotnet run --project src/EvoContext.Demo -- replay --run-id runbook_502_v1_20260316T103517Z_54ca

# CLI operator output
dotnet run --project src/EvoContext.Cli -- replay --run-id runbook_502_v1_20260316T103517Z_54ca
```

## Step 4 — Aggregate stats

```bash
dotnet run --project src/EvoContext.Cli -- stats --scenario <scenario_id>
```

Trace artifacts are stored at `artifacts/traces/{scenario_id}/{run_id}.json`.

## Troubleshooting

- **Verify resolved configuration** — run `dotnet run --project src/EvoContext.Cli -- config` to see all config keys as the app sees them. Secrets display as `***`, unset keys display as `<empty>`.
- `embed` exits `2`: Doc 6 appeared in top 3. Re-check the dataset path and re-run `embed`.
- Qdrant connection error: verify `QDRANT_URL` and `QDRANT_API_KEY`.
- `replay` artifact not found: verify run id format `{scenario_id}_{timestamp}_{4-char-guid}`.
- If a runbook score appears unexpectedly low, inspect `missing_step_labels` and `order_violation_labels` in the trace artifact.
