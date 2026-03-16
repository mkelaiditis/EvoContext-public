# Developer Guide

## System purpose

EvoContext is a testbed for agentic RAG with observable, deterministic scoring, designed to prove retrieval and evaluation correctness before adding product features.

## Architecture rationale

- _Deterministic scoring_: Model-based evaluation is non-deterministic and hard to gate on. Rule-based scoring against labeled fact lists makes Gate B hallucination detection automatable.
- _Validation gates_: Gate A (Doc 6 exclusion) and Gate B (20-run hallucination harness) must pass before the pipeline is trusted. No features are added until the relevant gate passes.
- _Two-run adaptive loop_: Run 1 scores the answer. If score is below threshold, Run 2 expands the query using missing fact labels and re-retrieves. This is a demonstration of adaptive memory, not a general retry policy.
- _Scenario hosting_: All production runs go through `run`, not internal commands. This ensures trace artifacts are always written and the renderer always receives live events.

## Pipeline overview

The pipeline flow is: embed query -> retrieve candidates -> score/rank/select -> pack context -> generate answer -> evaluate -> (if score is low and Run 2 is allowed) expand query -> retrieve again -> re-score.

## How to add a scenario

1. Create `data/scenarios/{id}/scenario.json` (see [Scenario Schema Reference](reference/scenario-schema.md)).
2. Add the document dataset at the path referenced by `dataset_path`.
3. Run `evocontext ingest` and `evocontext embed --scenario {id}`.
4. If the scenario requires a new evaluator type, follow the evaluator extension steps below.

## How to add an evaluator

1. Implement `IScenarioEvaluator` in `src/EvoContext.Core/Evaluation/`.
2. Register it in `ScenarioEvaluatorDispatcher` construction inside `src/EvoContext.Cli/Services/Run5ServiceFactory.cs` and `src/EvoContext.Cli/Services/CliCommandExecutor.cs`.
3. Add a `*ScenarioResultPayload` record to `src/EvoContext.Infrastructure/Models/TraceArtifact.cs` and handle it in `src/EvoContext.Cli/Services/TraceArtifactBuilder.cs` (`BuildScenarioResultPayload`).

### Worked example: `Runbook502Evaluator`

- Evaluator entry point: `src/EvoContext.Core/Evaluation/Runbook502Evaluator.cs`
- Component decomposition:
- `Runbook502StepEvaluator`
- `Runbook502HallucinationDetector`
- `Runbook502ScoreCalculator`
- `Runbook502QuerySuggestionMapper`
- `Runbook502RuleTables`
- Registration points:
- `src/EvoContext.Cli/Services/Run5ServiceFactory.cs`
- `src/EvoContext.Cli/Services/CliCommandExecutor.cs`
- Scenario result payload mapping:
- `src/EvoContext.Cli/Services/TraceArtifactBuilder.cs` (`Runbook502ScenarioResult` branch)

## Validation gates

- Gate A: `validation/EvoContext.Validation.GateA/` - Doc 6 must NOT appear in top K=3 retrieval. Exit `0` = pass, `2` = fail, `3` = infrastructure error.
- Gate B: `validation/EvoContext.Validation.GateB/` - 20 identical runs with hallucination detection. Fails when hallucination count is greater than `0`.
- Both gates must pass before embedding or retrieval parameters are changed.

## Trace artifacts and replay

See [Trace Artifacts Reference](reference/trace-artifacts.md).

`ReplayRenderer` reconstructs semantic events from the artifact and drives `IRunRenderer`, allowing any run to be replayed without re-executing the pipeline.

## Programmatic host extension

Use the CLI facade services as the single execution path and inject host presentation behavior instead of composing Infrastructure directly.

1. Build a host-specific renderer implementing `IRunRenderer`.
2. Inject renderer/stage-progress factories into `CliCommandExecutor` or `ScenarioRunner`:
3. `rendererFactory: Func<ILogger, IRunRenderer>`
4. `stageProgressReporterFactory: Func<ILogger, IStageProgressReporter>?`
5. For deterministic host tests, inject a custom Run5 executor delegate (same signature used by `Run5ServiceFactory.ExecuteRun5Async`) to avoid external dependencies.

Example shape:

```csharp
var runner = new ScenarioRunner(
	logger,
	configuration,
	() => screenLogger,
	rendererFactory: l => new MyHostRenderer(l),
	stageProgressReporterFactory: l => null);
```

Rules:

- Keep command parsing in host entrypoints, then call facade services.
- Do not create a second execution pipeline in hosts.
- Keep trace/event contracts unchanged; only swap renderer and progress UX.
