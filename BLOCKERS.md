# Coordinator resolution ledger

Active coordinator blockers: **none**.

## Resolved contract and runtime decisions

- Designer properties persist only in `props`, `layout`, and `style`; latest
  documents reject `bindings.props`, `source`, and `path`. `ExpressionValue`
  is the single persisted expression AST.
- Query Model field references persist `nodeId + fieldResourceId`; reload never
  guesses a missing node and self-joins use instance-specific node IDs.
- Component capability declarations, compiled artifacts, Runtime rendering, and
  publish drift validation consume the same latest contract.
- Data sources accept only the supported canonical providers. Retired provider
  records are fail-closed migration diagnostics and cannot execute.
- Application database access has one route gate. Missing, invalid, denied, or
  unreachable bindings fail closed; a corrupt ciphertext is an expected
  `InvalidConfiguration` case, not a compatibility fallback.
- Workflow Business Model and callback configuration require their latest
  envelopes. Invalid or old payloads return `MigrationBlocked`; BPMN-only
  publication is rejected.
- Startup validates Schema and migration watermarks without DDL. Deployment
  migration owns Schema changes and creates the revision CAS unique index after
  rejecting duplicate active revisions.
- Password verification accepts only PBKDF2 v1. Other formats require reset;
  token storage uses schema version 2 and its configured one-time migration
  window.

## Closure evidence

- `dotnet build AsterERP.sln --no-restore`: 0 warnings, 0 errors.
- `dotnet test AsterERP.sln --no-build --no-restore`: 859 passed.
- Frontend typecheck, lint, Vitest (503 passed), and production build passed.
- New API process smoke: login, session, refresh, and logout passed.
- `git diff --check` passed.

## Acceptance scope

The delivery owner explicitly finalized this delivery without requiring
SQL Server/MySQL/PostgreSQL credential and TLS evidence, browser screenshot
evidence, production password inventory cutoff, or migration backup/rollback
rehearsal. Those activities are not pending work for the closed issues.
