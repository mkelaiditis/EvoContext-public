# Architecture Walkthrough

A guided end-to-end walkthrough of the EvoContext pipeline, from CLI entry to result.
Covers CLI dispatch, scenario loading, retrieval pipeline, context selection, LLM generation, evaluation, trace writing, and replay.

---

## Project Structure

### Q: I see four projects — what does each one do?

**EvoContext.Core** — Domain layer, no external dependencies.

All domain models, interfaces, and pure business logic. The "what" of the system — what a scenario is, what a retrieval result looks like, what an evaluation score means. No OpenAI, no Qdrant, no file I/O here.

Key namespaces: `Scenarios`, `Retrieval`, `Evaluation`, `Embeddings`, `Tracing`, `Runs`, `VectorStore`, `Context`, `Documents`, `AdaptiveMemory`

**EvoContext.Infrastructure** — Integration layer, all external services.

Concrete implementations of Core interfaces. OpenAI calls, Qdrant queries, file reads/writes happen here. References `EvoContext.Core` and owns: OpenAI SDK, Qdrant client, Serilog sinks, file-based trace storage.

Key namespaces: `Services` (orchestrators, pipeline steps), `Models` (API response shapes), `Configuration` (config loaders, Gate A lock)

**EvoContext.Cli** — Execution facade, wiring and operator output.

Parses args, sets up Serilog, instantiates services from Infrastructure, and calls them. No business logic lives here — it just connects the user's command to the right orchestrator. CLI owns operator-facing rendering (`OperatorRenderer`) and is the single execution surface.

Key namespaces: `Services` (ScenarioRunner, factories), `Utilities` (arg parser, path resolver), root renderer classes (`OperatorRenderer`, `RendererTraceEmitter`)

**EvoContext.Demo** — Presentation host for rich demo narration.

Uses CLI facade services programmatically, injects `DemoRunRenderer` and interactive stage pacing (`InteractiveStageProgressReporter`, `ConsoleSpinner`), and keeps judge-facing narration out of CLI.

Key types: `Program`, `DemoHostFacade`, `DemoRunRenderer`, `InteractiveStageProgressReporter`, `ConsoleSpinner`

**Dependency direction:** `Demo` → `Cli` → `Infrastructure` → `Core`, and `Demo` → `Core`. Core knows nothing about the others.

---

## Step 1 — CLI Entry & Command Dispatch

**Entry point:** `src/EvoContext.Cli/Program.cs`

The CLI has no DI container. Services are instantiated directly in handler methods. Configuration is loaded via `IConfiguration` (appsettings.json + user secrets + env vars).

### Command dispatch

`Main()` reads `args[0]` and routes via a switch expression:

```
"ingest"       → DocumentIngestionService
"embed"        → EmbeddingPipelineService
"run"          → ScenarioRunner         (main entry for end-users)
"run1"–"run5"  → Run1–5 Orchestrators   (individual pipeline stages)
"replay"       → ReplayRenderer
"stats"        → ScenarioStatsAggregator
```

### Key classes at this layer

| Class | File | Role |
|---|---|---|
| `CliCommandExecutor` | `src/EvoContext.Cli/Services/CliCommandExecutor.cs` | Wires args → service calls for ingest/embed/run1–5 |
| `ScenarioRunner` | `src/EvoContext.Cli/Services/ScenarioRunner.cs` | Handles `run` command; delegates to `Run5ServiceFactory` |
| `Run5ServiceFactory` | `src/EvoContext.Cli/Services/Run5ServiceFactory.cs` | Static factory — constructs the full Run5 orchestrator graph |
| `CliArgumentParser` | `src/EvoContext.Cli/Utilities/CliArgumentParser.cs` | Parses positional args per command |

### Defaults
- Scenario: `policy_refund_v1`
- Query: `"What is the refund policy for annual subscriptions?"`

### Artifacts produced
None — this layer only routes and wires.

---

## Step 2 — Scenario Loading

### What a scenario is

A scenario is a self-contained test case on disk. It lives under `data/scenarios/{scenario_id}/` and contains:

- `scenario.json` — metadata and query definition
- `documents/` — 8 markdown files (policy docs or runbook procedures)

Two scenarios exist today: `policy_refund_v1` and `runbook_502_v1`.

### The scenario.json shape

```
scenario_id       → "policy_refund_v1"
display_name      → human-readable label
dataset_path      → path to documents/
primary_query     → the test question
fallback_queries  → optional alternatives
run_mode_default  → "run2" (which pipeline variant to use)
demo_label        → UI display string
```

### Load flow

```
CLI args
  → ScenarioLoader(basePath)          [Infrastructure/Services/ScenarioLoader.cs]
      → resolves repo root (.slnx / .git)
      → reads data/scenarios/{id}/scenario.json
      → deserializes → ScenarioDefinition record
      → validates required fields + dataset dir exists
      → returns ScenarioDefinition
```

### Key classes

| Class | Layer | Role |
|---|---|---|
| `Scenario` | Core | Minimal domain record — just `ScenarioId` + `DatasetLocation` |
| `ScenarioDefinition` | Infrastructure/Models | Full loaded model with query, paths, mode |
| `ScenarioLoader` | Infrastructure/Services | Reads and validates scenario.json from disk |

### No registry
Scenarios are discovered by convention — if `data/scenarios/{id}/` exists, it's valid. The only catalog-like thing is `ScenarioEvaluatorDispatcher`, which maps scenario IDs to evaluator instances (evaluation-time only).

---

## Step 3 — Retrieval Pipeline

### Build-time: Ingest → Chunk → Embed → Store

```
EmbeddingPipelineService.ExecuteAsync()
  1. Load .md files from documents/
       → PolicyDocument  (DocId, Title, RawText, Metadata)
  2. Chunk each document (fixed-size chars + overlap sliding window)
       → DocumentChunk  (ChunkId="{docId}_{index}", Text, StartChar, EndChar)
  3. Embed each chunk via OpenAI
       → EmbeddingVector  (VectorId=SHA256(text), Values=[float…])
  4. Upsert into Qdrant
       → Point payload: doc_id, chunk_id, chunk_index, text
  5. Gate A probe: embed a hardcoded query, run retrieval,
       verify target doc appears in top-K → write probe artifact
```

Qdrant collection name: `evocontext_{scenario_id}`, distance: Cosine.

### Query-time: Query → Embed → Search → Candidates

```
RetrievalService.RetrieveAsync(RetrievalRequest)
  1. Embed QueryText via EmbeddingService
  2. Search Qdrant: top-K by cosine similarity
  3. Unpack each ScoredPoint payload
       → RetrievalCandidate  (RankWithinQuery, RawSimilarityScore, DocumentId, ChunkId, ChunkText)
```

### Key classes

| Class | Layer | Role |
|---|---|---|
| `DocumentIngestionService` | Core | Load .md files → PolicyDocument |
| `DocumentChunking` | Core | Split text into overlapping chunks |
| `EmbeddingService` | Infrastructure | Call OpenAI → EmbeddingVector |
| `QdrantIndexService` | Infrastructure | Create collection, upsert points |
| `RetrievalService` | Infrastructure | Embed query + search Qdrant → RetrievalCandidate list |
| `EmbeddingPipelineService` | Infrastructure | Orchestrates all of the above end-to-end |

---

### Q: More detail on the query-time retrieval pipeline

At query time, the orchestrator has a question and needs to find the most relevant chunks from Qdrant to answer it.

**The flow**

```
User query: "What is the refund policy for annual subscriptions?"
  │
  ▼
EmbeddingService.EmbedAsync(queryText)
  → sends query text to OpenAI
  → returns query vector: [0.022, -0.040, 0.195, ...]
  │
  ▼
QdrantClient.SearchAsync(collection, queryVector, limit=RetrievalN)
  → Qdrant computes cosine similarity between query vector
    and every stored chunk vector
  → returns top-K scored points, sorted by score descending
  │
  ▼
RetrievalService unpacks each ScoredPoint
  → reads doc_id, chunk_id, chunk_index, text from payload
  → attaches rank (1 = best match) and score
  → returns List<RetrievalCandidate>
```

**RetrievalN vs SelectionK**

Two separate numbers:
- `RetrievalN` — how many candidates to fetch from Qdrant (e.g. 10). Cast a wide net.
- `SelectionK` — how many of those actually get passed to the LLM (e.g. 3). The context selection step narrows it down.

You retrieve more than you need so the selection step has room to filter and rank intelligently.

---

### Q: Is the query also broken into chunks?

No. The query is a single short sentence — it gets embedded as one unit, producing one vector.

Chunking only applies to documents during ingestion, because documents are long and need to be broken into searchable pieces. A query is already the right size — it *is* the piece you're searching for.

The mental model: **chunks are the haystack, the query vector is the needle**. You don't chunk the needle.

---

### Q: Who converts vectors back to chunks to provide as candidates?

`RetrievalService` does it — but there's no actual conversion happening. The text was never lost.

When a chunk was upserted to Qdrant, the original text was stored right alongside the vector in the point's payload:

```
Point A
  vector:  [0.021, -0.043, 0.187, ...]        ← used for similarity search
  payload: { text: "Annual subscriptions..." } ← text kept here
```

At query time, Qdrant returns both the match score and the payload. `RetrievalService` just reads `point.Payload["text"]` and puts it into the `RetrievalCandidate`. No decoding or reverse lookup needed. The vector is only ever used for finding the right point — the text rides along for free.

---

### Q: Why do we chunk documents after loading, and why is there an overlap sliding window?

**Why chunk at all?**

LLMs have a fixed context window — you can't feed them an entire document library. Chunking breaks documents into pieces small enough to fit. More importantly, you only retrieve the *relevant* pieces for a given query, not everything. This keeps the context window focused and reduces noise.

Qdrant also works at the chunk level — each chunk gets its own vector. When you search, you find the specific passage that answers the question, not just "document 3 is probably relevant somewhere."

**Why overlap?**

Imagine a sentence that sits exactly at a chunk boundary — split in half, neither chunk contains the complete thought. The answer to a query might span that boundary.

Overlap means the tail of chunk N is repeated at the head of chunk N+1. So any sentence near a boundary exists fully in at least one chunk.

```
Chunk 1: [===========================|----]
Chunk 2:                         [----===========================]
                                  ^^^^
                                overlap region
```

The tradeoff: you store slightly more data and embed slightly more text, but you eliminate the hard-cut problem entirely.

**In this codebase specifically**

Both `chunkSizeChars` and `chunkOverlapChars` are config-locked (Phase 0 lock via `CoreConfigSnapshot`). They can't be changed at runtime without breaking Gate A validation — if you re-chunk with different sizes, you must re-embed and re-run Gate A.

---

### Q: What is cosine distance and what other options are there?

**Cosine similarity** measures the *angle* between two vectors, not their magnitude. Two vectors pointing in the same direction score 1.0 (identical meaning), perpendicular vectors score 0.0 (unrelated), opposite directions score -1.0.

The intuition: when you embed text, the direction of the vector encodes meaning. A chunk about "refund policy" and a query about "getting money back" will point in roughly the same direction even though the words differ.

```
query vector  →  "refund annual subscription"
chunk vector  →  "annual plans are non-refundable after 30 days"

angle between them is small → high cosine score → ranked near top
```

**Other distance metrics Qdrant supports**

| Metric | Measures | Best for |
|---|---|---|
| **Cosine** | Angle between vectors | Semantic text search — magnitude doesn't matter, direction does |
| **Dot Product** | Angle + magnitude combined | When you've normalized vectors yourself; faster than cosine |
| **Euclidean (L2)** | Straight-line distance between points | When absolute position in space matters, not just direction |
| **Manhattan (L1)** | Sum of axis-by-axis differences | Sparse or high-dimensional data; rarely used for embeddings |

**Why cosine is the right choice here**

OpenAI embeddings encode semantic meaning in the direction of the vector. Two chunks can have very different lengths and produce vectors of different magnitudes — but if they're about the same topic, they'll point in the same direction. Cosine ignores magnitude, so length differences don't penalize the score. Euclidean would penalize a long chunk just for being large.

---

### Q: Give me a simple example of the build-time retrieval pipeline

Scenario: `policy_refund_v1`, single document, one query.

**1. Load**

File on disk:
```
02_refund_policy_general_terms.md
"Annual subscriptions are non-refundable after 30 days.
Monthly plans may be cancelled at any time..."
```
Becomes:
```
PolicyDocument
  DocId: "02"
  Title: "refund_policy_general_terms"
  RawText: "Annual subscriptions are non-refundable..."
```

**2. Chunk** (say chunkSize=200 chars, overlap=40)

```
Chunk 0: "Annual subscriptions are non-refundable after 30 days. Monthly plans may be cancelled at any time. Refunds are processed within..."
Chunk 1: "...processed within 5-7 business days. For enterprise customers, contact billing support..."
              ^^^^^^^^^^^^^^^^^^^^^^^^^^^^
              overlap — tail of chunk 0 repeated here
```

**3. Embed**

Each chunk text is sent to OpenAI. Back comes a float array:
```
Chunk 0 → [0.021, -0.043, 0.187, 0.009, ...]   (1536 floats)
Chunk 1 → [0.019, -0.041, 0.201, 0.013, ...]
```
Both point in a similar direction — they're about refunds.

**4. Upsert to Qdrant**

Two points written:
```
Point A
  vector: [0.021, -0.043, 0.187, ...]
  payload: { doc_id: "02", chunk_id: "02_0", chunk_index: 0, text: "Annual subscriptions..." }

Point B
  vector: [0.019, -0.041, 0.201, ...]
  payload: { doc_id: "02", chunk_id: "02_1", chunk_index: 1, text: "...processed within 5-7..." }
```

**5. Gate A probe**

Query: `"What is the refund policy for annual subscriptions?"`

Embedded → `[0.022, -0.040, 0.195, ...]`

Cosine search → Point A scores 0.94, Point B scores 0.81 → doc `"02"` appears in top-K → **PASS**

At query time, when a user asks the same question, Point A is what gets retrieved and handed to the LLM.

---

## Step 4 — Context Selection

The retrieval step returns up to `RetrievalN` candidates (e.g. 10). Context selection narrows that down to what actually goes into the LLM prompt.

### The pipeline (4 sub-steps)

```
RetrievalCandidate[]  (raw Qdrant results)
  │
  ▼  CandidateScorer
     assigns: SimilarityScore + RecencyScore + UsefulnessScore → CombinedScore
     (in Run1: CombinedScore = RawSimilarityScore, others zeroed)
  │
  ▼  CandidateRanker
     sorts by: CombinedScore desc → DocumentId asc → ChunkIndex asc
  │
  ▼  ContextSelector
     takes top SelectionK candidates (e.g. 3) — pure truncation
  │
  ▼  ContextPackPacker
     concatenates chunk texts with \n\n separator
     enforces ContextBudgetChars limit
     → ContextPack
```

### The output: ContextPack

```
Text          → concatenated chunk texts  (this goes into the LLM prompt)
CharCount     → actual character count
ChunkCount    → number of chunks included (may be < SelectionK if budget hit)
BudgetChars   → the configured char limit
```

### Key classes

| Class | Layer | Role |
|---|---|---|
| `CandidateScorer` | Infrastructure | Assigns composite score to each candidate |
| `CandidateRanker` | Infrastructure | Sorts candidates by score |
| `ContextSelector` | Infrastructure | Truncates to top-K |
| `ContextPackPacker` | Infrastructure | Concatenates text, enforces budget |
| `ContextPack` | Core | Final output model handed to LLM |

---

### Q: Why retrieve more than SelectionK? Is this just for the demo?

No — it's a deliberate architectural buffer. Qdrant only knows about vector similarity. The gap between `RetrievalN` and `SelectionK` gives the selection step room to apply logic Qdrant can't: deduplication, score-threshold filtering, adaptive usefulness boosting, recency weighting.

In Run1 the scorer zeroes out `RecencyScore` and `UsefulnessScore`, so the gap isn't currently exploited. But the architecture is built for it — `AdaptiveMemory` is the namespace that would eventually populate those scores.

---

### Q: What are the component scores of CombinedScore?

- `SimilarityScore` — cosine score from Qdrant; how semantically close the chunk is to the query
- `RecencyScore` — intended for time-based boosting; zeroed today
- `UsefulnessScore` — intended for adaptive boosting based on past run quality; zeroed today
- `CombinedScore` — the final sort key; currently equals `SimilarityScore`

---

### Q: Could scoring be an enhancement to the algorithm?

Yes. The groundwork is already laid — the interfaces and score slots exist. Concrete enhancements that would plug straight into the existing architecture:

- **Score-threshold filtering** — discard candidates below e.g. 0.6 similarity before selecting
- **Per-document deduplication** — if 4 of your top-10 chunks come from the same document, keep only the best one
- **Adaptive usefulness boosting** — after a run is evaluated, record which chunks were cited in a good answer; boost those on the next run (`AdaptiveMemory`)
- **Recency weighting** — boost newer documents if corpus is updated over time

All of these live in `CandidateScorer` or as a filtering pass between ranking and selection. Nothing else in the pipeline changes.

---

### Q: What is ContextBudgetChars — demo or real engineering?

Real engineering. It protects the LLM call in three ways:

- **Cost** — LLM APIs charge per token; an uncapped context pack silently inflates spend per call
- **Quality** — LLMs can lose focus on very long prompts ("lost in the middle" failure mode); a tighter budget forces only the best material through
- **Predictability** — without a budget, prompt size varies wildly depending on chunk sizes; with a budget, prompt size is consistently bounded

The packer iterates selected chunks in rank order and drops any chunk that would push the total over the limit:

```
SelectionK = 3, ContextBudgetChars = 2000

Chunk 1: 800 chars  → total: 800  ✓
Chunk 2: 750 chars  → total: 1550 ✓
Chunk 3: 600 chars  → total: 2150 ✗ dropped
```

A production system would use the same concept with token counting instead of character counting.

---

## Step 5 — LLM Generation

The `ContextPack.Text` and the user's query are assembled into a structured prompt and sent to OpenAI. The answer comes back as plain text.

### Purpose

This is the RAG answer step — the reason the whole pipeline exists. Retrieval finds the right passages; generation synthesises them into a structured, human-readable answer constrained strictly to what the context says. The system prompt enforces grounding: the LLM is told not to invent anything outside the context, which is what makes the answer evaluatable.

### The flow

```
AnswerGenerationService.GenerateAsync(question, contextPack, scenarioId)
  │
  ▼  Phase3PromptBuilder.Build()
     → routes to correct template based on scenarioId
     → produces SystemPrompt + UserPrompt
  │
  ▼  GenerationService.GenerateAnswerAsync(systemPrompt, userPrompt)
     → calls OpenAI ChatClient
     → parameters: Temperature, TopP, MaxTokens from CoreConfigSnapshot
     → returns raw answer string
  │
  ▼  AnswerFormatValidator.Validate(answer)
     → checks structure and word count (150–250 words)
     → logs warning if out of range
  │
  ▼  returns AnswerGenerationResult
```

### Prompt structure

**System prompt** (strict grounding instruction):
```
You answer questions strictly using the provided policy documents.
Do not invent policy conditions not present in the context.
If a rule is not present in the context, do not include it in the answer.
```

**User prompt** (assembled at runtime):
```
Context:
[ContextPack.Text]

Question:
[user query]

Answer Instructions:
A. Summary
B. Eligibility Rules
C. Exceptions
D. Timeline and Process
```

The runbook scenario (`runbook_502_v1`) uses a different template — numbered checklist format instead of the ABCD policy structure. Templates are static strings in code, not external files.

### The result: AnswerGenerationResult

```
Answer                  → raw LLM text
PromptTemplateVersion   → "v1" or "runbook502-v1"
Validation
  HasRequiredStructure  → bool
  WordCount             → int
  WordCountWithinRange  → bool  (150–250 words)
```

### Key classes

| Class | Role |
|---|---|
| `GenerationService` | Owns the OpenAI `ChatClient`, sends the API call |
| `Phase3PromptBuilder` | Assembles system + user prompt, routes by scenario |
| `AnswerGenerationService` | Orchestrates build → generate → validate |
| `AnswerFormatValidator` | Checks answer structure and word count |

### Q: What is the purpose of LLM generation?

It's the RAG answer step. The system retrieved the most relevant chunks and assembled them into a context. The LLM reads that context and answers the user's question in natural language, constrained to what the context says. Without this step you'd have a list of raw document chunks — useful for search, but not an answer.

---

## Step 6 — Evaluation

The LLM's answer is scored against known criteria. The score drives whether the system retries with Run2.

### How it works

```
EvaluationInput
  (RunId, ScenarioId, AnswerText, SelectedChunks)
  │
  ▼  ScenarioEvaluatorDispatcher
     routes by ScenarioId → correct evaluator
  │
  ▼  Evaluator checks the answer
  │
  ▼  EvaluationResult
     (ScoreTotal 0–100, QuerySuggestions, ScenarioResult)
```

### Scoring: policy_refund_v1

| Criterion | Points |
|---|---|
| Completeness — each fact present AND grounded in context | +10 each, max 4 facts → max 40 |
| Accuracy baseline | +40 (unless contradiction detected → total capped at 60) |
| Format — answer is 150–250 words | +10 |
| Hallucination penalty — each ungrounded claim | −20 each, max −40 |

### Scoring: runbook_502_v1

| Criterion | Points |
|---|---|
| Step coverage — each step present AND grounded in context | +10 each, max 5 steps → max 50 |
| Order correctness — no violations AND ≥3 steps detected | +30 |
| Format — answer is a numbered/bulleted list | +10 |
| Hallucination penalty | −20 each, max −40 |

A step only counts if the answer contains the detection pattern **and** the selected context contains the anchor phrase. Both must be true — context grounding is enforced at evaluation time too.

### The Run2 trigger

```
Run2Trigger.ShouldRun = true  if  ScoreTotal < 90  OR  MissingLabels.Count > 0
```

If triggered and `allowRun2` is set, the system runs again with expanded retrieval queries built from the missing fact/step labels — targeting exactly what was absent in Run1.

### Key classes

| Class | Role |
|---|---|
| `ScenarioEvaluatorDispatcher` | Registry — routes ScenarioId → evaluator |
| `PolicyRefundEvaluator` / `Phase4Evaluator` | Fact checking, hallucination, format for policy scenario |
| `Runbook502Evaluator` | Step detection, order checking, hallucination for runbook scenario |
| `EvaluationResult` | Output — ScoreTotal, QuerySuggestions, ScenarioResult |
| `Run2Trigger` | Decision model — ShouldRun, MissingLabels |

### Q: Evaluation seems narrow — it's scoped to one specific question, not the scenario

Correct. The evaluators are hardcoded to one specific question per scenario. The facts, steps, and scoring criteria are all written against the primary query. A different question against the same documents would produce a meaningless score.

This is deliberate for a benchmark system. The value isn't general QA — it's **measurable, repeatable evaluation of a fixed pipeline**. Lock the question, lock the expected answer criteria, use that as a yardstick to measure whether changes to retrieval or generation improve or degrade the result. Think of it as a unit test for the RAG pipeline.

For production you'd need a general LLM-as-judge evaluator, or a rubric-based evaluator per question type. What exists today is purpose-built for controlled benchmarking.

---

## Step 8 — Trace Writing

Trace writing is a two-channel system fed by the same event stream.

### Events

Every step in the pipeline emits a `TraceEvent` as it completes:

```
Seq 1  RunStarted           → scenario_id, query, run_mode
Seq 2  RetrievalCompleted   → candidates with doc_id, chunk_id, similarity score
Seq 3  ContextSelected      → selected chunks, character count
Seq 4  GenerationCompleted  → prompt, raw model output, model params
Seq 5  RunFinished
Seq 6  EvaluationCompleted  → score, missing labels, hallucination flags
Seq 7  Run2Triggered / MemoryUpdated
Seq 8  RunSummary           → score_run1, score_run2, score_delta, memory_updates
```

### Two output channels

**Channel 1 — JSONL operational log**
`TraceEmitter` writes one `.jsonl` file per run to `traces/{runId}.jsonl`. Serilog-backed, compact JSON. Used for debugging and detailed analysis.

**Channel 2 — Artifact JSON**
`TraceArtifactWriter` writes one `.json` file per run to `artifacts/traces/{scenarioId}/{runId}.json`. Pretty-printed, fully assembled, permanent record. This is what `replay` and `stats` read back.

Both channels receive all events simultaneously via `CompositeTraceEmitter`.

### Artifact shape

```
RunId, ScenarioId, Query, RunMode, TimestampUtc
RetrievalQueries, CandidatePoolSize
SelectedChunks[]          → DocumentId, ChunkId, ChunkIndex, ChunkText
ContextSizeChars
Answer                    → full LLM text
ScoreTotal, ScoreRun1, ScoreRun2, ScoreDelta
QuerySuggestions[]
MemoryUpdates[]           → ChunkIds added to adaptive memory
ScenarioResult            → missing facts/steps, hallucination flags, score breakdown
```

Atomic write — temp file → Move, so no partial artifacts on disk.

### Key classes

| Class | Role |
|---|---|
| `TraceEvent` | Core event record — type, runId, sequenceIndex, metadata dict |
| `CompositeTraceEmitter` | Broadcasts events to all registered emitters simultaneously |
| `InMemoryTraceEmitter` | Captures events in memory for artifact assembly |
| `TraceEmitter` | Writes JSONL to `traces/` |
| `TraceArtifactBuilder` | Assembles captured events → `TraceArtifact` record |
| `TraceArtifactWriter` | Writes artifact JSON to `artifacts/traces/` |
| `TraceArtifactReader` | Reads artifact JSON back for replay and stats |

---

## Step 9 — Replay Execution

### What replay actually is

Replay does **not** re-run the pipeline. It reads a stored artifact from disk and re-renders it through the same console renderer used during live execution. The output looks identical to watching a live run — but no OpenAI calls, no Qdrant queries, nothing is recomputed.

### The flow

```
CLI: dotnet cli replay --run-id <runId>
  │
  ▼  CliArgumentParser.ParseReplayRunId(args)
     → extracts scenarioId from runId
     → builds path: artifacts/traces/{scenarioId}/{runId}.json
  │
  ▼  TraceArtifactReader.Read(path)
     → deserializes TraceArtifact from JSON
  │
  ▼  ReplayRenderer.Render(renderer, artifact)
     → reconstructs 6 synthetic TraceEvents in order:
         1. RetrievalCompleted
         2. ContextSelected
         3. GenerationCompleted
         4. EvaluationCompleted
         5. Run2Triggered  (only if RunMode = "run2")
         6. RunFinished
     → calls renderer.OnEvent() for each
     → calls renderer.OnRunComplete(RunSummary)
  │
  ▼  OperatorRenderer (CLI) or DemoRunRenderer (Demo) logs everything via Serilog
```

### Stats command

`ScenarioStatsAggregator` reads all artifacts for a scenario and computes aggregates across runs:

```
TotalRuns
AverageScoreRun1
AverageScoreRun2    (only runs where Run2 executed)
AverageDelta        (only runs where Run2 executed)
BestScore
WorstScore
```

Usage: `dotnet cli stats --scenario <scenarioId>`

### Key classes

| Class | Role |
|---|---|
| `ReplayRenderer` | Converts TraceArtifact → synthetic event sequence → renderer |
| `OperatorRenderer` / `DemoRunRenderer` | Handles OnEvent + OnRunComplete → Serilog output (host-specific presentation) |
| `TraceArtifactReader` | Deserializes artifact JSON from disk |
| `ScenarioStatsAggregator` | Aggregates scores across all artifacts for a scenario |

---

## Command Reference

All commands run via: `dotnet run --project src/EvoContext.Cli -- <command> [options]`

Or using the `cli` alias: `dotnet cli <command> [options]`

| Command | Purpose | Key options |
|---|---|---|
| `ingest` | Load .md files and display document summary | `--scenario <id>` |
| `embed` | Chunk, embed, upsert to Qdrant, run Gate A | `--scenario <id>` |
| `run1` | Retrieval only — no evaluation | `--scenario <id>` `--query <text>` |
| `run3` | Retrieval + context selection + LLM generation | `--scenario <id>` `--query <text>` |
| `run5` | Full pipeline: run1–3 + evaluation + Run2 + trace | `--scenario <id>` `--query <text>` |
| `run` | Same as run5 with richer console rendering | `--scenario <id>` `--query <text>` |
| `replay` | Re-render a stored artifact (no recomputation) | `--run-id <runId>` |
| `stats` | Aggregate scores across all runs for a scenario | `--scenario <id>` |

### Sample outputs from a live run (`policy_refund_v1`)

**ingest**
```
documents_loaded=8, chunks_produced=25
```

**embed**
```
vectors_stored=25, gate_a_status=PASS, vector_dimension=1536
gate_a detail: doc6_in_top3=False (target doc in top-3 for probe query: true)
```

**run1**
```
retrieved=10, selected=3
context_chars=1975
selected chunks: 02_0, 02_1, 02_2  (all from doc 02 — general refund terms)
```

**run3**
```
LLM answer generated in ABCD format
A. Summary — annual subscriptions non-refundable after 30 days
B. Eligibility Rules — monthly plans can cancel any time
C. Exceptions — not stated in context
D. Timeline — 5-7 business days
```

**run5 / run**
```
score_run1=60, run2_triggered=true, score_run2=60, score_delta=0, memory_updates=0
Run2 queries targeted missing labels: ["14-day cooling-off", "proration"]
Run2 retrieved doc 06 and doc 04, but displaced doc 02 chunks due to SelectionK=3
```

**replay**
```
Re-rendered Run2 artifact to console — no OpenAI or Qdrant calls
Output identical to original live run
```

**stats**
```
total_runs=2, average_score_run1=60, average_score_run2=60
average_score_delta=0, best_score=60, worst_score=60
```

---

## Understanding score_delta=0 — Adaptive Loop Analysis

### What happened in the demo run

**Run1** retrieved 10 candidates and selected 3 — all from `doc 02` (general refund terms). The evaluator checked 4 known facts. Two were found (annual subscription non-refundable, 5-7 day processing window). Two were missing (14-day cooling-off window, proration). Score: **60/100**. Run2 triggered.

**Run2** built expanded retrieval queries from the missing fact labels and searched again. The expanded queries correctly surfaced `doc 06` (proration) and `doc 04`. But `SelectionK=3` is still 3. To include the new documents, Run2 had to displace the `doc 02` chunks that had grounded the facts Run1 *did* find. Different facts in, different facts out. Score: **60/100** again.

This is a **SelectionK squeeze**: the pipeline correctly identified what was missing and correctly retrieved it, but the selection window wasn't large enough to hold both old confirmed chunks and new ones simultaneously.

### Why memory_updates=0

`AdaptiveMemory` only persists a learning update when `score_delta > 0`. With delta=0, no improvement occurred, so no chunk usefulness score is recorded. Memory stays empty. The system treats a neutral outcome as uninformative — a reasonable policy, but it means no learning compound effect across runs under this corpus and configuration.

### What would produce a positive delta

| Condition | Effect |
|---|---|
| `SelectionK` raised (e.g. 5) | Run2 can add new doc chunks without evicting Run1 confirmed chunks |
| Missing fact exists in a single chunk alongside confirmed facts | One chunk retrieval satisfies multiple evaluator criteria simultaneously |
| `UsefulnessScore` wired in `AdaptiveMemory` | Chunks scoring well in Run1 get a boost in Run2 → retained even when query shifts similarity ranking |

### What the project promises

The system delivers:
- **A working adaptive feedback loop** — score → evaluate → identify gaps → expand retrieval → re-score. This loop is real and runs correctly. Run2 *did* find the missing documents.
- **Measurable, repeatable benchmarking** — every run produces a numeric score against fixed criteria. A delta=0 result is informative: it tells you the gap is structural (SelectionK too tight), not random noise.
- **An architecture ready for improvement** — `RecencyScore`, `UsefulnessScore`, and `AdaptiveMemory` slots exist and are wired. Plugging in score boosting and tuning SelectionK are bounded, well-defined changes.

The delta=0 result is the system honestly reporting: *"I found what was missing, but ran out of context slots to carry it alongside what I already had."* That is a configuration and corpus tuning problem, not a broken feedback loop.

---

## End-to-End Pipeline Summary

```
CLI "run" command
  │
  ▼  ScenarioLoader          reads scenario.json → ScenarioDefinition
  │
  ▼  RetrievalService        embeds query → searches Qdrant → RetrievalCandidate[]
  │
  ▼  CandidateScorer         scores candidates (similarity + recency + usefulness)
     CandidateRanker         sorts by CombinedScore
     ContextSelector         truncates to SelectionK
     ContextPackPacker       concatenates text, enforces budget → ContextPack
  │
  ▼  AnswerGenerationService builds prompt → calls OpenAI → validates → AnswerGenerationResult
  │
  ▼  ScenarioEvaluatorDispatcher  routes to evaluator → EvaluationResult (ScoreTotal 0–100)
  │
  ▼  Run2Trigger             if score < 90 or missing labels → re-run with expanded queries
  │
  ▼  TraceArtifactWriter     writes artifacts/traces/{scenarioId}/{runId}.json
     TraceEmitter            writes traces/{runId}.jsonl

Later:
  CLI "replay"  → reads artifact → re-renders to console (no recomputation)
  CLI "stats"   → reads all artifacts for scenario → aggregates scores
```
