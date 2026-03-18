# Runtime Guide

This guide describes how to observe EvoContext's behavior end to end. It covers both the live mode (OpenAI + Qdrant required) and offline replay mode (no external services required).

EvoContext runs the same query twice. Run 1 uses similarity-based retrieval. Run 2 uses evaluation feedback to expand retrieval. The goal of this walkthrough is to show how the system detects missing information and improves the answer in the second run.

---

## Expected Demo Outcome

A successful run shows all of the following:

- Run 1 score below the adaptive threshold (90)
- Run 2 triggered automatically
- Run 2 score higher than Run 1
- Recovered fact labels listed in the summary box
- Expansion queries visible in the trace artifact under `retrieval_queries`

If you see these five things, the adaptive loop worked as designed.

---

## Two Ways to Evaluate

| Mode | What it does | Requirements |
|---|---|---|
| **Live run** | Calls OpenAI and Qdrant in real time, produces a fresh trace | OpenAI API key, Qdrant instance, .NET 10 SDK |
| **Offline replay** | Reads a pre-recorded trace from disk and re-renders it | .NET 10 SDK only |

**If you want to see the system run live, follow sections A through D.**
**If you only want to observe the output and verify the logic, skip to section E.**

---

## Prerequisites

- .NET 10 SDK — [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
- For live mode only: an OpenAI API key and a running Qdrant instance

Qdrant can be started locally with Docker:
```bash
docker run -p 6333:6333 qdrant/qdrant
```

Alternatively, you can use a free-tier cluster on [Qdrant Cloud](https://cloud.qdrant.io). Create a cluster, then use the endpoint URL provided by the Qdrant Cloud UI in place of `http://localhost:6333` when setting `QDRANT_URL`.

---

## A. Environment Setup (live mode only)

**bash / Git Bash:**
```bash
export OPENAI_API_KEY="sk-..."
export QDRANT_URL="https://<your-cluster>.qdrant.io"   # or http://localhost:6333 for Docker
export QDRANT_API_KEY="<your-api-key>"                 # leave empty for local Docker
```

**PowerShell:**
```powershell
$env:OPENAI_API_KEY = "sk-..."
$env:QDRANT_URL     = "https://<your-cluster>.qdrant.io"   # or http://localhost:6333 for Docker
$env:QDRANT_API_KEY = "<your-api-key>"                     # leave empty for local Docker
```

To verify configuration is resolved correctly:
```bash
dotnet run --project src/EvoContext.Cli -- config
```

Expected output: a table of all config keys. Secrets show as `***`. Any key showing `<empty>` when it should be set indicates a missing variable.

---

## B. Dataset Setup — Embed (live mode only, run once)

This step loads documents, calls OpenAI to generate embeddings, and stores vectors in Qdrant. Run once per scenario. Does not need to be repeated unless you reset the Qdrant collection.

### B.1 Policy Refund scenario

```bash
dotnet run --project src/EvoContext.Cli -- embed --scenario policy_refund_v1
```

| What you see | What it means |
|---|---|
| Chunk counts per document | Documents were loaded and split into fixed-size chunks |
| `embed_completed` log line | Embeddings were generated and stored in Qdrant |
| Exit code `0` | Gate A passed: Doc 06 (early termination) did not appear in the top 3 similarity results for the refund query — the collection is correctly calibrated |
| Exit code `2` | Gate A failed — Doc 06 appeared in the top 3. Re-run `embed`. |

### B.2 Runbook 502 scenario

```bash
dotnet run --project src/EvoContext.Cli -- embed --scenario runbook_502_v1
```

| What you see | What it means |
|---|---|
| Chunk counts per document | Documents were loaded and split into fixed-size chunks |
| `embed_completed` log line | Embeddings were generated and stored in Qdrant |
| Exit code `0` | Gate A passed: the probe query did not surface the early-termination document in the top 3 similarity results — the collection is correctly calibrated |
| Exit code `2` | Gate A failed. Re-run `embed`. |

---

## C. Live Run

### C.1 Policy Refund scenario

```bash
dotnet run --project src/EvoContext.Demo -- run --scenario policy_refund_v1 --query "What is the refund policy for annual subscriptions?" --mode run2
```

#### Step-by-step output walkthrough

**1. Run 1 retrieval**
```
Retrieval completed: query_count=1 candidates=10
Query: What is the refund policy for annual subscriptions?
```
The system retrieved the top 10 candidates from Qdrant using the original query. Similarity-only search — no expansion yet.

**2. Context selection**
```
Context selected: chunks=3 context_chars=1955
Selected chunk ids: 02:02_0:0, 01:01_0:0, 03:03_0:0
```
3 chunks were selected from the 10 candidates and packed into the context window. The chunk IDs use the format `{doc_id}:{chunk_id}:{chunk_index}`.

**3. Run 1 answer generation**
```
Answer: [generated answer text]
```
The model answered using only the 3 selected chunks. At this point the answer is based on whatever the similarity search surfaced.

**4. Evaluation**
```
Evaluation completed: score_total=60
Missing items: MISSING_ANNUAL_PRORATION_RULE, MISSING_BILLING_ERROR_EXCEPTION,
               MISSING_PROCESSING_TIMELINE, MISSING_CANCELLATION_PROCEDURE
```
The deterministic evaluator checked the answer against 5 fact rules and 4 hallucination rules. Score 60/100 means 4 required facts are absent from the answer. These are not model judgements — they are pattern matches against the answer text with context anchors to confirm grounding.

**5. Run 2 triggered**
```
Run 2 triggered — score below threshold: expanded_queries=7
```
Score 60 is below the threshold of 90. The 4 missing fact labels were mapped to 7 targeted expansion queries. The system will now retrieve again using this richer query set.

**6. Run 2 retrieval**
```
Retrieval completed: query_count=7 candidates=14
```
7 queries total (1 original + 6 expansion). Up to 14 distinct candidates were retrieved and merged into a unified pool.

**7. Run 2 answer generation**
```
Answer: [improved answer text]
Evaluation completed: score_total=90
```
The expanded context surfaced the missing clauses. The model now has evidence for the prorated reimbursement rule, billing error exception, and processing timeline.

**8. Summary box**
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  RUN 1 RESULT
  Score: 60
  Missing items:
    * Annual subscription proration rule
    * Billing error refund exception
    * Refund processing timeline
    * Cancellation procedure

  Run 1 answer: [wrapped answer]
    Present:  14-day cooling-off window
    Missing:  Annual subscription proration rule, Billing error refund exception,
              Refund processing timeline, Cancellation procedure

  RUN 2 RESULT
  Score: 90
  Recovered items:
    + Annual subscription proration rule
    + Billing error refund exception
    + Refund processing timeline
  Still missing:
    * Cancellation procedure

  Run 2 answer: [wrapped answer]
    Present:  14-day cooling-off window, Annual subscription proration rule,
              Billing error refund exception, Refund processing timeline
    Missing:  Cancellation procedure

  Score improvement: +30
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

| Label | What it means |
|---|---|
| **Missing items** (Run 1) | Required facts the evaluator found absent from the Run 1 answer |
| **Recovered items** (Run 2) | Facts that were missing in Run 1 and present in Run 2 — direct evidence of adaptive improvement |
| **Still missing** | Facts not recovered even after Run 2 — the cancellation procedure requires a specific account-portal phrasing not present in the retrieved context |
| **Score improvement** | Numeric delta. +30 here means the adaptive pass materially improved completeness |

---

### C.2 Runbook 502 scenario

```bash
dotnet run --project src/EvoContext.Demo -- run --scenario runbook_502_v1 --query "The service returns 502. What do I do?" --mode run2
```

The runbook evaluator checks **step coverage**, not fact presence. A score is deducted for each required diagnostic step that is absent from the answer.

#### Step-by-step output walkthrough

**1. Run 1 retrieval**
```
Retrieval completed: query_count=1 candidates=9
Query: The service returns 502. What do I do?
```
The system retrieved 9 candidates from Qdrant using the original query. Similarity-only search — no expansion yet.

**2. Context selection**
```
Context selected: chunks=3 context_chars=2029
Selected chunk ids: 04:04_0:0, 01:01_0:0, 06:06_0:0
```
3 chunks were selected from the 9 candidates and packed into the context window.

**3. Run 1 answer generation**
```
Answer: 1. Check the health status of each dependent service...
        ...
        13. Monitor the P1 incident until service restoration is confirmed.
```
The model produced a 13-step triage procedure using the 3 selected chunks. Dependency health, log inspection, restart, and escalation steps are present. Deployment history inspection is not.

**4. Evaluation**
```
Evaluation completed: score_total=54
Missing items: Inspect recent deployments, Roll back faulty deployment
```
The deterministic evaluator checked the answer against the required runbook steps. Score 54/100 means 2 required steps are absent. These are structural checks against the answer text — not model judgements.

**5. Run 2 triggered**
```
Run 2 triggered — score below threshold: expanded_queries=3
```
Score 54 is below the threshold of 90. The 2 missing step labels were mapped to 3 targeted expansion queries. The system will retrieve again using this richer query set.

**6. Run 2 retrieval**
```
Retrieval completed: query_count=3 candidates=9
```
3 queries total (original + 2 expansion targeting deployment inspection and rollback). 9 candidates retrieved, different chunks selected — chunk 03 replaces chunk 01 from Run 1.

**7. Run 2 answer generation**
```
Answer: 1. Check the health status of each dependent service...
        ...
        10. Re-check the service health after the rollback.
Evaluation completed: score_total=90
```
The expanded context surfaced the deployment history and rollback procedure. Both missing steps are now present. The answer is more focused — 10 steps instead of 13, because the escalation path is no longer needed once rollback is covered.

**8. Summary box**
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  RUN 1 RESULT
  Score: 54
  Missing items:
    * Inspect recent deployments
    * Roll back faulty deployment

  Run 1 answer:
    Present:  Check upstream service health, Inspect service logs
    Missing:  Inspect recent deployments, Roll back faulty deployment

  RUN 2 RESULT
  Score: 90
  Recovered items:
    + Inspect recent deployments
    + Roll back faulty deployment
  Still missing: (none)

  Run 2 answer:
    Present:  Check upstream service health, Inspect service logs,
              Inspect recent deployments, Roll back faulty deployment
    Missing:  (none)

  Score improvement: +36
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

| Label | What it means |
|---|---|
| **Missing items** (Run 1) | Required runbook steps the evaluator found absent from the Run 1 answer |
| **Recovered items** (Run 2) | Steps that were missing in Run 1 and present in Run 2 — direct evidence of adaptive improvement |
| **Still missing** | Steps not recovered after Run 2. None in this case — full step coverage achieved. |
| **Score improvement** | Numeric delta. +36 here means the adaptive pass fully resolved the missing steps. |

---

## D. Trace Files Produced

After a live run, two files are written to `artifacts/traces/{scenario_id}/`:

### `{run_id}.json` — Run summary artifact

This is the primary trace. It contains everything needed to reconstruct and verify what happened.

| Field | What it records |
|---|---|
| `run_id` | Unique identifier: `{scenario_id}_{timestamp}_{4-char-guid}` |
| `query` | The original query text |
| `retrieval_queries` | All queries used across both runs. First entry is the original; subsequent entries are the expansion queries generated from missing fact labels. The expansion queries are direct evidence that Run 2 was driven by structured feedback. |
| `selected_chunks` | The 3 chunks that were packed into the Run 2 context window, including full text and document ID |
| `retrieval.run1.candidate_documents` | Ranked document IDs retrieved for Run 1 before context selection |
| `retrieval.run2.candidate_documents` | Ranked document IDs retrieved for Run 2 before context selection |
| `score_run1` / `score_run2` / `score_delta` | Scores for each pass and the improvement delta |
| `scenario_result.present_fact_labels` | Facts confirmed present in the final answer |
| `scenario_result.missing_fact_labels` | Facts still absent after Run 2 |
| `scenario_result.hallucination_flags` | Any hallucinated claims detected. Empty list means no hallucinations were flagged. |
| `scenario_result.score_breakdown` | `completeness_points` (up to 50), `format_points` (up to 10 or 20 depending on scenario), `hallucination_penalty` (deducted per flag), `accuracy_cap_applied` |
| `detected_evidence_items` | For each present fact: the document it came from, the anchor phrase that matched, and the extracted snippet. This shows the evaluator's reasoning. |
| `memory_updates` | Chunk IDs recorded as useful by the adaptive memory store for future runs |

**To verify adaptive improvement in the trace:** compare `retrieval_queries[0]` (original query) with `retrieval_queries[1..n]` (expansion queries). The expansion queries are generated directly from the missing fact labels — not from the model. Then compare `score_run1` and `score_run2`.

### `{run_id}.verification.json` — Run 1 snapshot

This file captures the state before Run 2. It exists so you can verify what Run 1 actually retrieved and answered without the adaptive pass.

| Field | What it records |
|---|---|
| `run1.answer` | The verbatim Run 1 answer — the one that scored below threshold |
| `run1.selected_chunks` | The 3 chunks available to the model in Run 1 — compare these to the `selected_chunks` in the main trace to see how retrieval changed |

**To confirm that retrieval changed between runs:** check that `selected_chunks` in `.verification.json` (Run 1) differs from `selected_chunks` in the main `.json` (Run 2). Different chunks = the adaptive query expansion reached documents that similarity-only search missed.

---

## E. Offline Replay (no API keys required)

Replay reads a stored trace from disk and re-renders the full demo output. It does not call OpenAI or Qdrant. Pre-recorded traces for both scenarios are included in `docs/samples/`.

> **Run all commands from the repo root.**

**bash / Git Bash:**

```bash
mkdir -p artifacts/traces/policy_refund_v1 artifacts/traces/runbook_502_v1
cp docs/samples/policy_refund_v1_20260316T215650Z_c39f.json artifacts/traces/policy_refund_v1/
cp docs/samples/runbook_502_v1_20260316T215710Z_a40c.json artifacts/traces/runbook_502_v1/
dotnet run --project src/EvoContext.Demo -- replay --run-id policy_refund_v1_20260316T215650Z_c39f
dotnet run --project src/EvoContext.Demo -- replay --run-id runbook_502_v1_20260316T215710Z_a40c
```

**PowerShell:**

```powershell
New-Item -ItemType Directory -Force -Path artifacts\traces\policy_refund_v1, artifacts\traces\runbook_502_v1 | Out-Null
Copy-Item docs\samples\policy_refund_v1_20260316T215650Z_c39f.json artifacts\traces\policy_refund_v1\
Copy-Item docs\samples\runbook_502_v1_20260316T215710Z_a40c.json artifacts\traces\runbook_502_v1\
dotnet run --project src/EvoContext.Demo -- replay --run-id policy_refund_v1_20260316T215650Z_c39f
dotnet run --project src/EvoContext.Demo -- replay --run-id runbook_502_v1_20260316T215710Z_a40c
```

The output is identical to a live run. The same summary box, the same scores, the same recovered/missing items.

### What replay proves and what it does not prove

| Replay proves | Replay does not prove |
|---|---|
| The demo narrative is accurate — scores, missing items, and recovered items are real | That the system can call OpenAI and Qdrant right now (that requires a live run) |
| The trace format is well-structured and contains full run state | That the run was produced in this session |
| The evaluator logic is deterministic — given the same trace, the same output is always produced | |
| The improvement delta in the trace artifact is real | |

---

## F. Troubleshooting

| Symptom | Fix |
|---|---|
| `embed` exits `2` | Gate A failed — Doc 06 appeared in top 3. Delete the Qdrant collection and re-run `embed`. |
| Qdrant connection error | Verify `QDRANT_URL` is set and Qdrant is running. Run `dotnet run --project src/EvoContext.Cli -- config` to see resolved values. |
| `replay` artifact not found | Check the run ID format: `{scenario_id}_{yyyyMMddTHHmmssZ}_{4-char-guid}`. Verify the file exists at `artifacts/traces/{scenario_id}/{run_id}.json`. |
| Score appears lower than expected on live run | Model output may vary slightly between runs. The evaluator is deterministic and will always score the same answer the same way. The included sample traces show the expected improvement on a recorded run. |
