<!--
Source of truth: src/EvoContext.Infrastructure/Models/ScenarioDefinition.cs
If ScenarioDefinition fields change, update this document accordingly.
-->

# Scenario Schema

Scenarios are defined as JSON files at `data/scenarios/{scenario_id}/scenario.json`.

## Fields

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `scenario_id` | `string` | Yes | Unique identifier, used as collection name suffix and in run IDs. |
| `display_name` | `string` | Yes | Human-readable label shown in screen output. |
| `dataset_path` | `string` | Yes | Path to the document dataset, relative to repo root or absolute. |
| `primary_query` | `string` | Yes | Default query used when `--query` is not supplied to `run`. |
| `fallback_queries` | `string[]` | Yes | Alternative queries used in Run 2 adaptive expansion. |
| `run_mode_default` | `string` | Yes | Default run mode (`"run1"` or `"run2"`). |
| `demo_label` | `string` | Yes | Label displayed in demo screen output. |

## Example: policy_refund_v1

```json
{
  "scenario_id": "policy_refund_v1",
  "display_name": "Policy Refund Q&A",
  "dataset_path": "data/scenarios/policy_refund_v1/documents",
  "primary_query": "What is the refund policy for annual subscriptions?",
  "fallback_queries": [],
  "run_mode_default": "run2",
  "demo_label": "Primary Scenario - Policy Q&A"
}
```

Note: The current `policy_refund_v1` example uses an empty `fallback_queries` array. This is valid for the current scenario data, but the field is still used by Run 2 adaptive query expansion and can be populated for scenarios that define explicit fallback queries.

## Collection naming

The Qdrant collection for a scenario is always `evocontext_{scenario_id}`.
