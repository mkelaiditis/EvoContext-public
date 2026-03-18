# EvoContext

**Adaptive RAG that knows when it is wrong.**

EvoContext is a prototype agentic retrieval-augmented generation system. It detects when a generated answer fails a deterministic evaluation rubric and automatically runs a second retrieval pass with an expanded query — producing a measurably better answer.

> This is a hackathon prototype. It demonstrates the core adaptive loop on two scenarios. It is not a production system.

---

## The Problem

Standard RAG retrieves documents by similarity to the original query and generates an answer. If the query is ambiguous or the relevant clause lives in a semantically distant document, retrieval misses it — silently. The answer looks complete but isn't. There is no feedback loop.

## What EvoContext Does Differently

1. **Run 1 — Similarity retrieval + generation.** Retrieves top candidates, selects context, generates an answer.
2. **Evaluation.** A deterministic rule-based evaluator checks the answer against a rubric — required facts, ordering constraints. Produces a score and a list of missing items. No model judge, no hallucination risk in the evaluator itself.
3. **Run 2 — Adaptive retry.** If the score is below threshold, EvoContext expands the query using the missing items as signal, retrieves again from a different angle, and generates an improved answer.
4. **Tracing.** Every run produces a canonical JSON trace artifact capturing both passes — inputs, retrieval results, scores, and final answers — so the improvement is inspectable and reproducible.

The improvement is measurable: scores are numeric, the delta between Run 1 and Run 2 is shown, and missing items are named.

---

## Architecture

The diagram below shows the core EvoContext loop: the system answers a query once, evaluates the answer against a deterministic rubric, and retries retrieval if required information is missing.

```
Query
  |
  v
[Run 1: Similarity Retrieval]
  |   top-N candidates -> select K -> pack context -> generate answer
  |
  v
[Deterministic Evaluator]
  |   rule-based rubric -> score + missing item labels
  |
  +-- score >= threshold --> done
  |
  +-- score < threshold
        |
        v
      [Run 2: Adaptive Retrieval]
          expanded query (original + missing item signal)
          -> retrieve -> rerank -> select K -> generate answer
          -> re-evaluate -> final score
```

**Stack:** C# / .NET 10 · OpenAI `text-embedding-3-small` + `gpt-4.1` · Qdrant (cosine similarity) · Serilog

Validation gates block pipeline changes: Gate A verifies retrieval precision; Gate B stress-tests hallucination resistance across 20 runs. Evidence from both gates is committed to the repo.

---

## Demo Scenarios

| Scenario | Query | Run 1 | Run 2 |
|---|---|---|---|
| `policy_refund_v1` | "What is the refund policy for annual subscriptions?" | Misses cooling-off window clause | Recovers missing clause, score improves |
| `runbook_502_v1` | "The service returns 502. What do I do?" | Misses required diagnostic step | Completes the procedure, score improves |

---

## Quick Start — Deterministic Replay (Recommended)

Replay is the recommended evaluation path. It reads pre-recorded trace artifacts and re-renders the full demo output deterministically. No API keys, no external services, no variability.

Run all commands from the repo root.

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

Expected outcome: Run 2 score higher than Run 1, recovered fact labels listed, score improvement shown.

For full output walkthrough, see the [Runtime Guide](docs/runtime-guide.md).

---

## Optional — Live Execution (Requires API Keys)

> **Results may vary.** Live runs call OpenAI for embeddings and generation. Embedding output is not perfectly deterministic across runs, which means Run 2 improvement is not guaranteed on every fresh embed. Use replay for reproducible evaluation.

Live execution requires an OpenAI API key and a running Qdrant instance. Follow the [Runtime Guide](docs/runtime-guide.md) sections A through D.

---

## Evaluation Guides

- [Runtime guide](docs/runtime-guide.md) — how to run the demo and interpret the output
- [Source review guide](docs/source-review-guide.md) — how the pipeline, evaluator, and adaptive loop work
- [Demo scenarios](docs/demo-scenarios.md) — what each dataset demonstrates

---

## Repository Structure

```
src/EvoContext.Core/           Core contracts and domain models
src/EvoContext.Infrastructure/ OpenAI and Qdrant implementations
src/EvoContext.Cli/            CLI host (embed, run, replay, stats)
src/EvoContext.Demo/           Demo host with rich narrated output
data/scenarios/                Scenario document datasets
docs/                          Operator and developer guides
docs/samples/                  Pre-recorded trace artifacts for replay
validation/                    Gate A (retrieval precision) and Gate B (hallucination stress test)
tests/                         Unit and regression tests
```

---

## What This Is Not

- Not a general-purpose RAG framework.
- No UI, API, or persistent user state.
- Evaluation rubrics are scenario-specific; generalizing them is future work.
- The adaptive loop currently runs at most two passes.

---

## Future Directions

The prototype demonstrates that a deterministic evaluation signal can drive adaptive retrieval. Interesting open questions include how this pattern generalizes across evaluation rubric types, how the query expansion step can be made more robust, and whether the tracing artifacts can support offline analysis at scale.

---

## License

Business Source License 1.1. Source is available for review, research, and evaluation.
Commercial use is restricted until 2030-01-01, after which the code converts to Apache 2.0.
See [LICENSE](LICENSE).
