# AsterERP Code Hygiene Cleanup Plan

## Goal

Deliver a repeatable cleanup workflow for unused backend/frontend code and structural rule violations without changing public API contracts, database schema, permissions, routes, or runtime behavior.

## Execution Order

1. Generate backend and frontend audit reports with the scripts under `scripts/code-hygiene`.
2. Fix the existing frontend typecheck/lint baseline before using deletion candidates as evidence.
3. Confirm backend entity candidates through reference counts, SqlSugar usage, CodeFirst initialization, tests, and workflow persistence boundaries.
4. Confirm frontend candidates across FULL, MES, and WMS reachability graphs.
5. Delete only candidates marked `safe-delete-candidate` after build/test proof.
6. Refactor structural findings in controlled batches: overlong files first, then multi-type files, then Helper/Adapter/Facade/Shim naming.
7. Record every decision in `docs/code-hygiene-audit.md`.

## Non-Negotiable Constraints

- Do not delete target-only frontend alias files.
- Do not delete or migrate database tables in this cleanup pass.
- Do not change API paths, DTO wire shapes, permission codes, menu routes, or ABP module boundaries.
- Do not add Bridge, Adapter, Facade, or Shim layers to carry business logic.
- Keep source-only commits separate from runtime logs and SQLite files.

## Verification

- Backend: `dotnet build AsterERP.sln` and `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj`.
- Frontend: `npm run typecheck`, `npm run lint`, `npm run build`.
- Reachability: `npm run publish:reachability`, plus MES/WMS target reachability with `VITE_APP_TARGET_APP_CODE`.
