# Public Repo Setup Checklist

Before pushing to the public submission repo, complete the following steps.

## Files to commit

### Sample trace artifacts

Copy from the private repo and commit to the public repo under the same paths:

```
docs/samples/policy_refund_v1_20260311T151244Z_21b1.json
docs/samples/runbook_502_v1_20260316T103517Z_54ca.json
```

These are referenced by `docs/operator-guide.md` Step 3 (offline replay). Judges cannot replay without them.

### Trace artifact directory stubs

The `replay` command resolves artifacts at `artifacts/traces/{scenario_id}/{run_id}.json`. The subdirectories must exist for the copy commands in the guide to work on a fresh clone. Add a `.gitkeep` to each:

```
artifacts/traces/policy_refund_v1/.gitkeep
artifacts/traces/runbook_502_v1/.gitkeep
```

## Content fixes pending in operator-guide.md

Three minor content issues to fix before publishing:

1. **`QDRANT_API_KEY` is optional for local Qdrant** — add a note next to it in the Configuration section so judges running a local instance know they can leave it blank.

2. ~~**Policy scenario default query is not shown**~~ — **Fixed**: both demo run commands now include explicit `--query` arguments so judges see the full input.

3. **Copy step missing explanation** — Step 3 instructs judges to copy files before replaying but does not explain why. Add one sentence: replay looks for artifacts under `artifacts/traces/`, so the samples must be copied there first.

## Linux compatibility (untested — validate before publishing)

.NET 10 is cross-platform and the source should compile on Linux, but this has never been tested. If a judge runs on Linux and hits a build or runtime failure, it reflects poorly on the submission.

Before publishing, validate on the available Linux instance:

- [ ] `dotnet build` completes with 0 errors
- [ ] `dotnet test` passes
- [ ] Offline replay end-to-end: copy sample, run Demo replay, confirm output
- [ ] Check console spinner output — `ConsoleSpinner` uses `Console.IsOutputRedirected` and writes escape sequences; verify behaviour in a Linux terminal
- [ ] Check file path separators — code uses `Path.Combine` throughout which is cross-platform, but verify no hardcoded backslashes remain in config or test fixtures

If Linux validation is not done before publishing, add a note to the operator guide Prerequisites stating the build has been validated on Windows only.

## Verify before push

- [ ] `dotnet build` — 0 warnings, 0 errors
- [ ] `dotnet test` — all tests pass
- [ ] Offline replay works from a clean directory: copy a sample, run `dotnet run --project src/EvoContext.Demo -- replay --run-id <id>`, confirm rich output renders
- [ ] Remove any secrets, API keys, or personal config from appsettings or committed files
- [ ] Linux compatibility validated (see section above) — or Prerequisites note added if not done
