# AsterERP Low-Code Studio Definition of Done

## Coordinator decision

Status: **Blocked — not eligible for final Done/sign-off**.

This is an evidence matrix, not a release approval. `In Review` tickets are
development-complete only; they must not be changed to `Done` until the
unified integration gates below pass.

## Evidence already present

| Area | Evidence | Result |
| --- | --- | --- |
| Latest-only source guard | old directory scan, production token scan and `LatestOnly` tests | Pass, current worktree rerun 7/7 after `70fe13316`; no forbidden production semantic or versioned formal contract was reported. |
| Backend build | `dotnet build AsterERP.sln --no-restore` | Pass, 0 warnings/errors |
| Backend regression | `AsterERP.Api.Tests` | Pass for current branch slice: full Release run 725/725 after `85c00e16`; final product gate remains In Review |
| Frontend regression | Vitest | Pass for current branch slice: 81 files / 393 tests after `61631d1c`; `npm run typecheck` and `npm run build` also pass |
| Frontend gates | typecheck, lint, build | Pass |
| Shared runtime capability contract | backend Contracts canonical JSON, embedded backend consumer, generated frontend artifact and drift test | Pass, component/action/converter unknowns rejected at publish and runtime boundaries |
| Runtime parity | `offline-parity-regression.md` | In Review |
| Runtime acceptance | `runtime-acceptance.md` plus artifact rollback closed loop | Pass for automated contract/rollback cases; external runtime still blocked |
| Data Studio acceptance | `data-studio-acceptance.md` plus SQLite evidence | Pass for local SQLite 15/15; four-provider external gate still blocked |
| Security gates | HAO-107 test suite | Pass, 10/10 automated cases |
| Monitoring contract | 13 latest event names and typed context | In Review |
| Restarted local API + browser smoke | Release API at `http://127.0.0.1:5000` with Development + `Database:SkipSeed=true`, Vite in-app browser at `http://127.0.0.1:5173` | Partial, authenticated browser verified the latest DesignerDocument designer route, Save draft action, Preview Runtime route, and Data Studio data-source route; Data Studio business data remains Blocked because seed data was intentionally skipped |

## Blocking evidence

The following are not available in this workspace and cannot be simulated:

1. Authorized maintenance window, operator, backup snapshot, health check,
   publish pointer, and rollback rehearsal.
2. Real SQL Server, MySQL, PostgreSQL, and SQLite containers with authorized
   credentials, including catalog, DDL, DML, cancellation, timeout, and view
   replacement traces.
3. Authorized API/UI end-to-end traces after API restart, including permission
   deny, tenant/application boundaries, audit chain, and browser accessibility,
   visual, and responsive evidence.
4. Complete authoritative mapping of every HAO-13..113 ticket to its final
   commit and acceptance evidence. The latest Linear live read is recorded in
   `linear-live-status-2026-07-12.json`; per-ticket implementation evidence is
   still being appended to the affected issues.
5. Stable Release performance evidence across repeated runs; the latest full
   frontend run passed all 100/500/1000/2000-node scenarios with 2000-node
   save p95 96.363 ms, undo p95 38.280 ms, and runtime first-screen p95
   61.466 ms. Repeated controlled Release evidence is still required before
   sign-off.

The local Release backend was ultimately started with `Database__SkipSeed=true`
after the full Development seed chain blocked before opening the HTTP socket.
This is an environment-only recovery measure; it explicitly means seeded Data
Studio business data is not acceptance evidence and is not a production
migration or release approval.

## Required release decision

Keep HAO-5, HAO-114, phase parents, and affected quality gates open. Keep
implementation-complete tickets in `In Review`. Do not claim project complete,
delete migration inputs, or execute production replacement until all blockers
have evidence and the final deletion scan is rerun against the release commit.

## Rollback pointer

The current implementation branch is `codex/low-code-studio-latest`. Every
completed development slice has its own commit; rollback must restore the
last approved artifact/database snapshot from the runbook, never resurrect a
deleted legacy runtime or compatibility route.
