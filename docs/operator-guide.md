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

Run all commands from the repo root. Sample artifacts for both scenarios are included in `docs/samples/`:

| File | Scenario | Run mode | Score Run 1 | Score Run 2 | Delta |
|---|---|---|---|---|---|
| `policy_refund_v1_20260316T215650Z_c39f.json` | Policy refund | run2 | 60 | 90 | +30 |
| `runbook_502_v1_20260316T215710Z_a40c.json` | Runbook 502 | run2 | 54 | 90 | +36 |

**PowerShell:**

```powershell
New-Item -ItemType Directory -Force -Path artifacts\traces\policy_refund_v1, artifacts\traces\runbook_502_v1 | Out-Null
Copy-Item docs\samples\policy_refund_v1_20260316T215650Z_c39f.json -Destination artifacts\traces\policy_refund_v1\ -Force
Copy-Item docs\samples\runbook_502_v1_20260316T215710Z_a40c.json -Destination artifacts\traces\runbook_502_v1\ -Force
dotnet run --project src/EvoContext.Demo -- replay --run-id policy_refund_v1_20260316T215650Z_c39f
dotnet run --project src/EvoContext.Demo -- replay --run-id runbook_502_v1_20260316T215710Z_a40c
```

**bash / Git Bash:**

```bash
mkdir -p artifacts/traces/policy_refund_v1 artifacts/traces/runbook_502_v1
cp docs/samples/policy_refund_v1_20260316T215650Z_c39f.json artifacts/traces/policy_refund_v1/
cp docs/samples/runbook_502_v1_20260316T215710Z_a40c.json artifacts/traces/runbook_502_v1/
dotnet run --project src/EvoContext.Demo -- replay --run-id policy_refund_v1_20260316T215650Z_c39f
dotnet run --project src/EvoContext.Demo -- replay --run-id runbook_502_v1_20260316T215710Z_a40c
```

For CLI operator output instead of the Demo host:

```bash
dotnet run --project src/EvoContext.Cli -- replay --run-id runbook_502_v1_20260316T215710Z_a40c
```

## Step 4 — Aggregate stats

```bash
# Policy refund
dotnet run --project src/EvoContext.Cli -- stats --scenario policy_refund_v1

# Runbook 502
dotnet run --project src/EvoContext.Cli -- stats --scenario runbook_502_v1
```

To override the default K from the relevance profile:

```bash
dotnet run --project src/EvoContext.Cli -- stats --scenario policy_refund_v1 --k 5
```

Trace artifacts are stored at `artifacts/traces/{scenario_id}/{run_id}.json`.

### Retrieval Diagnostics Output

The `stats` command always emits `retrieval_diagnostics_status` and uses one of three states:

- `available`: includes `retrieval_diagnostics_k`, per-run metrics (`hit_at_k`, `recall_at_k`, `mrr`, `ndcg_at_k`), and delta fields.
- `unavailable`: includes `retrieval_diagnostics_reason` when profile or retrieval trace inputs are missing or malformed.
- `not_computable`: includes `retrieval_diagnostics_reason` when inputs are valid but metric math is undefined (for example, zero relevant documents).

Required fields by state:

- `available`: `retrieval_diagnostics_k`, run1/run2 metric fields, and delta fields.
- `unavailable` and `not_computable`: `retrieval_diagnostics_reason`.

Troubleshooting note for unavailable status:

- If `retrieval_diagnostics_status=unavailable`, verify the scenario has a valid `data/scenarios/{scenario_id}/relevance_profile.json` and that the latest trace artifact includes `retrieval.run1.candidate_documents` and `retrieval.run2.candidate_documents`.

## Troubleshooting

- **Verify resolved configuration** — run `dotnet run --project src/EvoContext.Cli -- config` to see all config keys as the app sees them. Secrets display as `***`, unset keys display as `<empty>`.
- `embed` exits `2`: Doc 6 appeared in top 3. Re-check the dataset path and re-run `embed`.
- Qdrant connection error: verify `QDRANT_URL` and `QDRANT_API_KEY`.
- `replay` artifact not found: verify run id format `{scenario_id}_{timestamp}_{4-char-guid}`.
- If a runbook score appears unexpectedly low, inspect `missing_step_labels` and `order_violation_labels` in the trace artifact.
