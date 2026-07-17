# Deterministic Golden Case runner

`frontend/AsterERP.Web/scripts/goldenCases.test.ts` is the executable companion to
`golden-cases.json`. It reads the anonymized fixtures, sends them through the
existing document codec/runtime kernel/command bus/binding resolver, and asserts
the resulting state and diagnostics. It does not modify `frontend/src`, the
backend application layer, the fixed debug SQLite database, or Flyfish assets.

Run it with:

```text
npm test --prefix frontend/AsterERP.Web -- --run scripts/goldenCases.test.ts --reporter=verbose
```

The runner covers the following input → validation → state/result paths:

| Case | Executed assertions | Result |
| --- | --- | --- |
| GC-001 | page fixture parse and schema validation → runtime artifact signing → kernel snapshot; an unknown component manifest is rejected | Pass |
| GC-002 | form/dataset binding resolution → `runMicroflow` action execution → deterministic order-id and affected-row result | Pass with external evidence blocked |
| GC-003 | serialize/reload → explicit revision and same-payload artifact hash stability → editor session fields absent | Pass |
| GC-004 | 1,000-node tree → pointer update remains uncommitted until pointer-up → one command-bus write and exact coordinate round-trip | Pass |
| GC-005 | 100-column/10,000-row optimistic-concurrency input → stale version rejected, zero affected rows, secret omitted from public projection | Pass with external evidence blocked |
| GC-006 | signed artifact mutation → integrity verification rejects tampered content and does not create a runtime kernel | Pass |

The two explicit `Pass with external evidence blocked` results are deliberate:
the deterministic runner cannot prove an authenticated Data Studio provider,
real affected-row/audit persistence, or secret-redaction behavior at the API
boundary without changing the prohibited production scope or using external
credentials. UI screenshot requirements from `golden-cases.json` are also
Blocked until an authorized browser run supplies the required case/viewport/state
images. These are acceptance-evidence blockers, not silently converted asset
existence assertions.
