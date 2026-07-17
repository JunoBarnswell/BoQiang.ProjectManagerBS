# AsterERP Code Hygiene Audit

## Baseline

- Backend source scan covers C# files under `backend`, excluding `bin` and `obj`.
- Frontend reachability scan covers TS/TSX files under `frontend/AsterERP.Web/src`, excluding tests and declaration files for deletion decisions.
- Current working tree includes runtime log/database noise; cleanup changes must stay source-only.

## Backend Entity Candidate Decisions

| Candidate | Evidence | Decision | Verification |
| --- | --- | --- | --- |
| `IHistoricScopeInstanceEntity` in `backend/AsterERP.Workflow.Persistence/Entities/IHistoricScopeInstanceEntity.cs` | Code search found only the interface definition. | Deleted as isolated workflow persistence contract. | `dotnet build AsterERP.sln`; workflow tests if available. |
| `VariableScopeImpl` in `backend/AsterERP.Workflow.Persistence/Entities/VariableScopeImpl.cs` | Code search found only the local `IVariableScope` declaration and `VariableScopeImpl : IVariableScope`; Workflow.Core has separate `IVariableScope` contracts. | Deleted as isolated persistence implementation and duplicate contract. | `dotnet build AsterERP.sln`; workflow tests if available. |

## Frontend Reachability Decisions

| Candidate | Evidence | Decision |
| --- | --- | --- |
| `src/app/router/workspaceRoutes.target.tsx` | Target build alias for MES/WMS publish reachability. | Keep by convention. |
| `src/app/navigation/routes.target.ts` | Target build alias for MES/WMS publish reachability. | Keep by convention. |
| `src/core/i18n/messages.target.ts` | Target build alias for MES/WMS publish reachability. | Keep by convention. |
| `src/apps/runtimeRegistry.*.ts` | Legacy runtime registry aliases. | Removed from the latest publish source boundary; no production source may reintroduce them. |

## Structural Hotspots

| Area | Evidence | Decision |
| --- | --- | --- |
| `DevelopmentSeedDataService.cs` | Over 2000 lines, mixes seed orchestration and multiple seed domains. | Refactor as seed contributors in a dedicated cleanup batch. |
| `FlowiseExecutionService.cs` | Over 2000 lines, mixes execution orchestration, node execution, state, audit, and error handling. | Refactor into execution coordinator and domain services in a dedicated cleanup batch. |
| `ApplicationDevelopmentCenterService.cs` | Over 2000 lines, mixes app config, pages, versions, shared resources, and publish checks. | Refactor into application service slices in a dedicated cleanup batch. |
| Contract aggregate files | Multiple DTO records per file by feature contract. | Split only when public namespace/type identity remains unchanged. |
| Workflow engine helper files | Vendored/engine-style code contains helper naming and dense command files. | Treat as legacy engine boundary unless a direct business bug requires changes. |

## Current Verification State

- `npm run typecheck`: Pass after the latest-only frontend boundary cut.
- `npm run lint`: Pass after the latest-only frontend boundary cut.
- Backend build/test: Pass, 647/647 after source changes.
