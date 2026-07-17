# Phase 0 performance and wide-table baseline

This baseline is a quality gate, not a placeholder result. Each scenario records the commit, toolchain, working-tree state, raw command, raw samples, p50/p95/p99, peak working set, runtime artifact size, and failure rate. The designer benchmark runs six times and reports the remaining five after warm-up.

## Executable collection

```powershell
pwsh -File docs/low-code-refactor/phase0-performance.ps1 -Action ValidatePlan
node docs/low-code-refactor/phase0-performance.mjs
pwsh -File docs/low-code-refactor/phase0-performance.ps1 -Action PrepareWideTable -Rows 10000 -Columns 100
```

The Node runner invokes the current low-code studio benchmark and writes four raw evidence files under `artifacts/phase0/performance/`. `PrepareWideTable` only creates a provider-specific evidence template and remains `Blocked` until an approved provider connection, seeded dataset, query plan, and authorized credentials exist. It never fabricates a result.

| Scenario | Boundary | Pass SLO | Required evidence |
|---|---:|---:|---|
| document parse/validate | 100/500/1000/2000 nodes | p95 <= 50/150/300/700 ms | raw samples, commit, toolchain, CPU, memory |
| canvas interaction | 100/500/1000/2000 nodes | p95 <= 16.7 ms/frame | authorized browser trace and screenshot |
| wide-table browse | 100 columns x 10,000 rows | first page p95 <= 2 s | provider, seed hash, SQL, query plan, returned rows |
| million-row browse | 20 columns x 1,000,000 rows | first page p95 <= 3 s | same evidence and working set independent of total rows |
| query cancel | SQL Server/MySQL/PostgreSQL/SQLite | server stop p95 <= 2 s | provider cancellation trace and failure rate |

Wide-table execution must use an explicit projection, bounded page size, and database-side filtering. It must not load the complete table into memory. Missing real provider/container/credential/query-plan evidence is `Blocked`.
