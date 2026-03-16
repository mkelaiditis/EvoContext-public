# Gate A Validation Runner

This project runs the Gate A semantic distance validation for the controlled policy dataset.

## Determinism Notes

- Documents are loaded in ordinal filename order (01_ to 08_).
- Chunking uses fixed size/overlap: 1200/200 characters.
- Qdrant collection is recreated on each run before indexing.
- Retrieval results are ranked by score, then `doc_id`, then `chunk_id` to break ties.
- Output formatting uses invariant culture and fixed score precision.

## Configuration

Non-secret values live in `appsettings.json`:

- `QDRANT_URL`
- `QDRANT_COLLECTION`

Secrets are loaded from user-secrets or environment variables:

- `OPENAI_API_KEY`
- `QDRANT_API_KEY`

## Run

```bash
dotnet run --project validation/EvoContext.Validation.GateA
```
