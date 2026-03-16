# Gate A Evidence

Run date: 2026-03-06

## Command

```bash
dotnet run --project validation/EvoContext.Validation.GateA
```

## Output

```
Gate A Validation Results
Question: What is the refund policy for annual subscriptions?
Results Returned: 10

Ranked Results:
 1. doc_id=02 chunk_id=2 score=0.498411
 2. doc_id=02 chunk_id=0 score=0.495820
 3. doc_id=02 chunk_id=1 score=0.474026
 4. doc_id=08 chunk_id=1 score=0.355216
 5. doc_id=04 chunk_id=0 score=0.345209
 6. doc_id=01 chunk_id=0 score=0.344143
 7. doc_id=08 chunk_id=0 score=0.335232
 8. doc_id=05 chunk_id=0 score=0.327030
 9. doc_id=07 chunk_id=1 score=0.325907
10. doc_id=06 chunk_id=1 score=0.324226

Doc 6 Rank: 10
Doc 6 Score: 0.324226

Gate A Result: PASS
```

## Artifact

See console output above for ranked results and PASS/FAIL status.

---

Run date: 2026-03-11 (Phase 7 verification)

## Command

```bash
dotnet run --project validation/EvoContext.Validation.GateA/EvoContext.Validation.GateA.csproj
```

## Output

```
Gate A Validation Results
Question: What is the refund policy for annual subscriptions?
Results Returned: 10

Ranked Results:
 1. doc_id=02 chunk_id=2 score=0.498195
 2. doc_id=02 chunk_id=0 score=0.495801
 3. doc_id=02 chunk_id=1 score=0.473992
 4. doc_id=08 chunk_id=1 score=0.355167
 5. doc_id=04 chunk_id=0 score=0.345207
 6. doc_id=01 chunk_id=0 score=0.344137
 7. doc_id=08 chunk_id=0 score=0.335238
 8. doc_id=05 chunk_id=0 score=0.326994
 9. doc_id=07 chunk_id=1 score=0.326285
10. doc_id=06 chunk_id=1 score=0.324386

Doc 6 Rank: 10
Doc 6 Score: 0.324386

Gate A Result: PASS
```

## Notes

- Gate A remains passing after Phase 7 loader rename and dataset-loader abstraction wiring changes.
