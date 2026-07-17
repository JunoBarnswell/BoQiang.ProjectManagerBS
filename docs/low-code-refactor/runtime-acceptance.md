# Runtime latest implementation acceptance

## Scope

This record covers the single current `RuntimeKernel` chain:

`RuntimeArtifact -> integrity/manifest verification -> RuntimeKernel -> ComponentRuntimeHost -> ActionHandlerRegistry`.

The acceptance does not retain a second renderer, version route, shadow
renderer, compatibility shim, or editor-session input at runtime.

## Automated and local evidence

- Backend publish/runtime/schema guard selection: 47/47 passed in the recorded
  local run; rerun command is `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LowCode|FullyQualifiedName~Runtime|FullyQualifiedName~ApplicationPublishRuntimeContractTests"`.
- Frontend RuntimeKernel, action registry, scope, dependency graph, manifest,
  and offline parity selection: 9 files / 41 tests passed in the recorded
  local run; the executable offline slice is
  `src/pages/application-console/development-center/low-code-studio/testing/offline/lowCodeOfflineParityRegression.test.ts`.
- Offline parity covers canonical document/hash, artifact hash/signature,
  properties, bindings, layout order, action result, unknown component/action/
  binding rejection, and artifact tampering.
- Runtime-only guard coverage now also executes the canonical binding conversion
  pipeline and derives the supported compiler revision from the shared runtime
  capability contract; direct unknown/invalid converter use fails closed.
- Latest-only deletion guard: the current worktree rerun is 7/7 after
  `70fe13316`; rerun with
  `pwsh -File docs/low-code-refactor/scan-latest-only.ps1` against every
  release commit.

These are local automated/local-browser evidence only. The browser evidence
currently demonstrates the local restarted API/Vite path and must not be
reported as production authorization, rollback, accessibility, or provider
evidence.

| Required slice | Current state | Required retained evidence |
| --- | --- | --- |
| Local RuntimeKernel/artifact/offline tests | EvidencePresent | Vitest JSON plus backend TRX from the commands above |
| Local authorized browser smoke | EvidencePresent | Restarted Release API/Vite trace showing load, integrity rejection and runtime preview |
| API/UI authorization and tenant boundary | Blocked | Authorized allow/deny traces for tenant/app, audit correlation and screenshots |
| Publish/rollback/restart recovery | Blocked | Maintenance window, backup hash, health check, rollback pointer and restore rehearsal |
| Accessibility/visual/responsive | Blocked | Browser keyboard/screen-reader/layout evidence at required viewports |
| Obsolete-entrypoint deletion | Blocked until rerun | Final release-commit deletion scan with retained output |
| Designer preview/four-resolver parity (HAO-91/92) | EvidencePresent | The Page Studio canvas posts the current latest `DesignerDocument` to the permission-protected preview-artifact endpoint; the backend compiler, `RuntimeArtifactCodec`, and `RuntimeKernel` use the same resolved properties, bindings, layout, and component registry as formal Runtime. Remaining gate is external visual/interaction evidence, not a second preview compiler |

## Acceptance result

`In Review`. The local code and offline acceptance cases are review evidence,
not release approval. `Done` is prohibited until authorized API/UI smoke
traces, production publish and rollback rehearsal, browser
accessibility/layout evidence, and the final obsolete-entrypoint scan are
available. Those external cases remain `Blocked` in
`runtime-release-blocked.md`.
