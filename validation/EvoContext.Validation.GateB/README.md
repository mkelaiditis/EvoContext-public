# Gate B Hallucination Stress Test Harness

This project runs the Gate B hallucination stress test against the controlled policy dataset.

## Determinism Notes

- Documents are loaded in ordinal filename order (01_ to 08_).
- Chunking uses fixed size/overlap: 1200/200 characters.
- Retrieval uses top N=10 and top K=3 with a 2,200 character context budget.
- Generation uses the locked model with temperature 0 and max tokens 350.
- F2 detection uses whole-word/phrase matching with grounding anchors.
- Output orders context doc IDs using ordinal sorting and formats rates with invariant culture.

## Configuration

Non-secret values live in `appsettings.json`:

- `QDRANT_URL`
- `QDRANT_COLLECTION`

Secrets are loaded from user-secrets or environment variables:

- `OPENAI_API_KEY`
- `QDRANT_API_KEY`

## Run

```bash
dotnet run --project validation/EvoContext.Validation.GateB
```
