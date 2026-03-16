<!--
Source of truth: src/EvoContext.Infrastructure/Models/TraceArtifact.cs
If TraceArtifact fields change, update this document accordingly.
-->

# Trace Artifacts

Trace artifacts are written to `artifacts/traces/{scenario_id}/{run_id}.json` after every `run` command.

Run ID format: `{scenario_id}_{yyyyMMddTHHmmssZ}_{4-char-guid}`.

## Top-level fields

| JSON property | Type | Nullable | Description |
| --- | --- | --- | --- |
| `run_id` | `string` | No | Unique run identifier. |
| `scenario_id` | `string` | No | Scenario identifier for this run. |
| `dataset_id` | `string` | No | Dataset identifier captured for the run. |
| `query` | `string` | No | Primary query used for the run. |
| `run_mode` | `string` | No | Effective run mode recorded in the artifact. |
| `timestamp_utc` | `string` | No | UTC timestamp for artifact creation. |
| `retrieval_queries` | `string[]` | No | Queries used during retrieval. |
| `candidate_pool_size` | `int` | No | Number of retrieved candidates before selection. |
| `selected_chunks` | `TraceArtifactSelectedChunk[]` | No | Selected grounding chunks. |
| `context_size_chars` | `int` | No | Character count of the final context pack. |
| `answer` | `string` | No | Generated answer text. |
| `score_total` | `int` | No | Final evaluation score for the recorded run mode. |
| `query_suggestions` | `string[]` | No | Suggested follow-up or expanded queries from evaluation. |
| `score_run1` | `int` | No | Run 1 score. |
| `score_run2` | `int` | Yes | Run 2 score when Run 2 executed; otherwise null. |
| `score_delta` | `int` | Yes | Score delta (`score_run2 - score_run1`) when Run 2 executed; otherwise null. |
| `memory_updates` | `string[]` | No | Memory update identifiers. |
| `scenario_result` | `object` | No | Scenario-specific evaluation payload (polymorphic). |

## selected_chunks object

Each entry in `selected_chunks` has these fields:

| JSON property | Type | Nullable | Description |
| --- | --- | --- | --- |
| `document_id` | `string` | No | Source document identifier. |
| `chunk_id` | `string` | No | Selected chunk identifier. |
| `chunk_index` | `int` | No | Chunk index within the source document. |
| `chunk_text` | `string` | No | Raw chunk text captured in the trace artifact. |

## scenario_result payload shapes

`scenario_result` is polymorphic and depends on scenario id.

### policy_refund_v1

- `missing_fact_labels` (`string[]`)
- `hallucination_flags` (`string[]`)
- `score_breakdown`:
  - `completeness_points` (`int`)
  - `format_points` (`int`)
  - `hallucination_penalty` (`int`)
  - `accuracy_cap_applied` (`bool`)

### runbook_502_v1

- `missing_step_labels` (`string[]`)
- `order_violation_labels` (`string[]`)
- `score_breakdown`:
  - `step_coverage_points` (`int`)
  - `order_correct_points` (`int`)
  - `hallucination_penalty` (`int`)

## Null and empty behavior

- `score_run2` and `score_delta` are null when only Run 1 executed.
- `memory_updates` is empty when Run 2 did not execute or produced no updates.
