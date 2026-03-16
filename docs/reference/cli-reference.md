<!--
Source of truth: src/EvoContext.Cli/Utilities/CliHelpText.cs
If CLI behavior changes, update this document accordingly.
-->

# CLI Reference

This document is the complete reference for the EvoContext CLI command surface.

## Role in architecture

- `EvoContext.Cli` is the execution facade used by both terminal operators and programmatic hosts.
- CLI default rendering is `OperatorRenderer` (structured key/value style with raw evaluator codes).
- Rich demo narration is intentionally not part of CLI and is hosted in `EvoContext.Demo`.

## ingest

Run document ingestion.

| Argument | Required/Optional | Default | Description |
| --- | --- | --- | --- |
| `--scenario <id>` | Optional | `policy_refund_v1` | Scenario identifier. |
| `--dataset <path>` | Optional | None | Dataset path override (optional). |

Exit codes:
- `0`: Ingestion completed successfully.
- `2`: Scenario/dataset resolution failed or ingestion execution failed.

Example:

```bash
evocontext ingest --scenario policy_refund_v1
```

## embed

Run embedding ingestion and Gate A probe.

| Argument | Required/Optional | Default | Description |
| --- | --- | --- | --- |
| `--scenario <id>` | Optional | `policy_refund_v1` | Scenario identifier. |
| `--dataset <path>` | Optional | None | Dataset path override (optional). |

Exit codes:
- `0`: Embedding ingestion completed and Gate A probe passed.
- `2`: Gate A probe failed (`Doc6InTop3=true`), argument/usage validation failed, or embedding execution failed.

Example:

```bash
evocontext embed --scenario policy_refund_v1
```

## run

Run a scenario demo.

| Argument | Required/Optional | Default | Description |
| --- | --- | --- | --- |
| `--scenario <id>` | Required | None | Scenario identifier. |
| `--query <text>` | Optional | Scenario primary query | Query text (optional; defaults to scenario primary query). |
| `--mode <run1|run2>` | Required | None | Run mode (`run1` or `run2`). |

Exit codes:
- `0`: Scenario run completed successfully.
- `2`: Missing/invalid arguments, scenario resolution error, or run execution error.

Example:

```bash
evocontext run --scenario policy_refund_v1 --mode run2
```

## run1

Run Phase 1 retrieval.

**Internal command:** This is a pipeline development command and is not part of the standard operator workflow.

| Argument | Required/Optional | Default | Description |
| --- | --- | --- | --- |
| `--scenario <id>` | Optional | `policy_refund_v1` | Scenario identifier. |
| `--query <text>` | Optional | `What is the refund policy for annual subscriptions?` | Query text. |
| `--repeat <n>` | Optional | `1` | Repeat runs to check determinism (minimum effective value is `1`). |

Exit codes:
- `0`: Run 1 execution completed successfully.
- `2`: Run 1 execution failed.

Example:

```bash
evocontext run1 --scenario policy_refund_v1 --repeat 1
```

## run3

Run Phase 3 answer generation.

**Internal command:** This is a pipeline development command and is not part of the standard operator workflow.

| Argument | Required/Optional | Default | Description |
| --- | --- | --- | --- |
| `--scenario <id>` | Optional | `policy_refund_v1` | Scenario identifier. |
| `--query <text>` | Optional | `What is the refund policy for annual subscriptions?` | Query text. |
| `--repeat <n>` | Optional | `1` | Repeat runs to check determinism (minimum effective value is `1`). |

Exit codes:
- `0`: Run 3 execution completed successfully.
- `2`: Run 3 execution failed.

Example:

```bash
evocontext run3 --scenario policy_refund_v1 --repeat 1
```

## run4

Run Phase 4 evaluation and scoring.

**Internal command:** This is a pipeline development command and is not part of the standard operator workflow.

| Argument | Required/Optional | Default | Description |
| --- | --- | --- | --- |
| `--input <path>` | Required | None | Evaluation input JSON path. |

Exit codes:
- `0`: Run 4 evaluation completed successfully.
- `2`: Missing `--input`, unsupported scenario output shape for CLI rendering, or run 4 execution failed.

Example:

```bash
evocontext run4 --input artifacts/phase4-input.json
```

## run5

Run Phase 5 adaptive memory (Run 1 + Run 2).

**Internal command:** This is a pipeline development command and is not part of the standard operator workflow.

| Argument | Required/Optional | Default | Description |
| --- | --- | --- | --- |
| `--scenario <id>` | Required | None | Scenario identifier. |
| `--query <text>` | Required | None | Query text. |
| `--repeat <n>` | Optional | `1` | Repeat runs to check determinism (minimum effective value is `1`). |

Exit codes:
- `0`: Run 5 execution completed successfully.
- `2`: Missing required arguments or run 5 execution failed.

Example:

```bash
evocontext run5 --scenario policy_refund_v1 --query "What is the refund policy for annual subscriptions?"
```

## replay

Replay a run.

| Argument | Required/Optional | Default | Description |
| --- | --- | --- | --- |
| `--run-id <id>` | Required | None | Run identifier. |

Exit codes:
- `0`: Replay rendered successfully from the trace artifact.
- `2`: Missing `--run-id`, invalid run id format, artifact not found, or replay failed.

Example:

```bash
evocontext replay --run-id policy_refund_v1_20260310T111249Z_da7a
```

## stats

Show run stats.

| Argument | Required/Optional | Default | Description |
| --- | --- | --- | --- |
| `--scenario <id>` | Required | None | Scenario identifier. |

Exit codes:
- `0`: Stats aggregation completed successfully.
- `2`: Missing `--scenario`, trace directory/data problems, or stats aggregation failed.

Example:

```bash
evocontext stats --scenario policy_refund_v1
```

## config

Show resolved configuration with secrets masked.

Prints all known configuration keys as resolved by the .NET configuration API (appsettings → user secrets → environment variables). Keys containing `KEY`, `SECRET`, or `PASSWORD` are displayed as `***`. Keys with no resolved value are displayed as `<empty>`.

No arguments.

Exit codes:
- `0`: Always succeeds.

Example:

```bash
evocontext config
```

Sample output:

```
Resolved configuration:
OPENAI_API_KEY = ***
QDRANT_URL = http://localhost:6333
QDRANT_API_KEY = <empty>
QDRANT_COLLECTION = evocontext-gate-a
Phase0:EmbeddingModel = text-embedding-3-small
Phase0:GenerationModel = gpt-4.1
...
```
