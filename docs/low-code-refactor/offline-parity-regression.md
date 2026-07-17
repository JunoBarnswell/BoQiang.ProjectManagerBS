# HAO-98 Offline parity regression

## Scope

`lowCodeOfflineParityRegression.test.ts` exercises the current offline chain without a network, mock runtime result, compatibility track, bridge, or shadow renderer:

`DesignerDocument → canonical JSON/document hash → RuntimeArtifact integrity hash/signature → RuntimeKernel`

The success case compares the preview projection and the published kernel snapshot for resolved properties, page bindings, responsive layout, style, and child order. It executes the registered runtime action and compares its result with the preview dispatch result. Negative cases assert fail-closed behavior for unknown component manifests, unknown actions, unsupported binding sources, and artifact content tampering.

## Cases

| Case | Expected evidence |
| --- | --- |
| HAO-98-P-001 | Same props, binding value, layout override, style, and child order in preview/runtime |
| HAO-98-P-002 | Same `setVariable` action result in preview/runtime path |
| HAO-98-N-001 | Unknown component rejected with `unknownManifest` |
| HAO-98-N-002 | Unknown action rejected with `unknownAction` |
| HAO-98-N-003 | Unknown binding source rejected with `invalidBinding` |
| HAO-98-N-004 | Changed artifact content rejected with `artifactTampered` before kernel load |

## Verification

```powershell
cd frontend/AsterERP.Web
npm run test -- --run src/pages/application-console/development-center/low-code-studio/testing/offline/lowCodeOfflineParityRegression.test.ts
npm run build
```

Pass means the named Vitest file is green and the frontend build succeeds. A failed command is Fail; inability to install dependencies or start the test runner is Blocked and must retain the command output as evidence.
