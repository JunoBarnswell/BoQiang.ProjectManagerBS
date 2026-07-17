# Latest-only deletion audit: HAO-101 / HAO-102 / HAO-104

Audited branch: `codex/low-code-studio-latest`
Audited commit: `0c617731`
Semantic policy: **latest-only**

## Audit result

The recorded historical source scan passed for the protected production roots. The old
generic renderer, old designer renderer/codec, old registries, numeric formal
contract, and the unused publish sidebar have deletion commits and are absent
from the working tree.
The exact evidence and deletion reasons are machine-readable in
`latest-only-deletion-audit.json`.

The current working-tree rerun is **Pass (7/7)** after `70fe13316`; the
previous intermediate `legacy openModal` diagnostic is no longer present in
the production compiler. The scan must still be rerun against the frozen
release commit after all remaining business changes land.

The routed designer and runtime page now use the latest document/session,
Inspector, Workflow Binding, and runtime-kernel chain. The unused legacy
`PublishSidebarPanel.tsx` was removed, so the production `full-designer` import
set is empty. The backend page contract now persists only `structured`, and the
publish source collector rejects legacy `runtimeRegistry.*` inputs and unsafe
source paths. The runtime artifact rollback endpoint now has its own signed
artifact validation and immutable audit tests. External runtime evidence and
the final release-commit scanning gate are still required before HAO-104 can
close.

## Evidence commands

```powershell
dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LatestOnly"
git diff --check
```

The scan intentionally permits migration/rejection inputs and test fixtures;
those are inputs to the latest migration, not runtime entry points. It does
not permit a versioned formal contract, numeric document routing, legacy
runtime imports, generic registry entry points, or the removed semantic names.

## Deletion evidence

- `bec698a97d45f0d04be7646f4ad8ba5ebe57d280`: deleted generic runtime entry
  points and registries.
- `c00f1c08efbee432475d1e8288a0cc1ee01f8270`: deleted the old designer
  renderer and runtime document codec.
- `f3226f825c4eb8b48babcb03b11b128d49c1ba0b`: replaced the numeric formal contract
  with `designer-document.latest.schema.json`.

Rollback restores the previous approved artifact/database snapshot from the
runbook. It must not resurrect the deleted renderer, parser, compiler branch,
or compatibility route.

## External blockers

Maintenance-window authorization, operator credentials, production API/UI
traces, four-provider database traces, and the final release-commit scan are
still required. No anonymous 401, mock, or placeholder result is accepted as
evidence.
