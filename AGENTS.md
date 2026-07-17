# Repository Guidelines

## Project Layout
- Backend solution (`AsterERP.sln`), ASP.NET Core Web API targeting .NET 10:
  - `backend/AsterERP.Api` — Web host: Controllers, Application, Infrastructure, Modules.
  - `backend/AsterERP.Shared` — Cross-cutting response models, pagination protocol, error codes, and permission infrastructure (no SqlSugar, no business I/O).
  - `backend/AsterERP.Contracts` — Request/response DTOs (`AsterERP.Contracts.*`); references Shared only.
  - `backend/AsterERP.Domain` — ORM entity base (`EntityBase` in `AsterERP.Domain.Common`); references SqlSugarCore only.
  - `backend/AsterERP.Workflow.Tools` — Shared utilities for the workflow approval engine (helpers, paging, FTP, Excel).
  - `backend/AsterERP.Workflow.Approval.Api` — Workflow approval DTO/model layer (not a web host); form and workflow data contracts.
  - `backend/AsterERP.Workflow.Approval.Core` — Workflow approval business services, repositories, configuration, and listeners.
  - `backend/AsterERP.Workflow.Forms.Api` — Form definition and data DTOs for the workflow form subsystem.
  - `backend/AsterERP.Workflow.Forms.Core` — Form services and repositories built on Flow.Core.
  - `backend/AsterERP.Workflow.Common` — Shared workflow engine primitives (exceptions, extensions).
  - `backend/AsterERP.Workflow.BpmnModel` — BPMN 2.0 object model for process definitions.
  - `backend/AsterERP.Workflow.BpmnParser` — BPMN XML parsing into the BpmnModel.
  - `backend/AsterERP.Workflow.Persistence` — Workflow entity layer and SqlSugar data managers.
  - `backend/AsterERP.Workflow.Core` — Process engine runtime (commands, deploy, history, jobs).
  - `backend/AsterERP.Workflow.Api` — Workflow service interfaces consumed by Flow.Core.
  - `backend/AsterERP.Workflow.DependencyInjection` — DI registration (`AddAsterERPWorkflow*`) and SqlSugar persistence adapters.
  - `backend/AsterERP.Api.Tests` — xUnit tests; references Api, Shared, Contracts, and Domain.
  - HTTP wiring belongs in `Endpoints` (Controllers).
  - Business orchestration belongs in `Application`.
  - Persistence, security, logging, database, and integrations belong in `Infrastructure`.
- Frontend: `frontend/AsterERP.Web`, Vite + React + TypeScript.
  - App bootstrap, routing, query setup, and environment wiring belong in `src/app` and `src/core`.
  - Route pages belong in `src/pages/<feature>`.
  - Reusable UI, forms, tables, auth, dictionary, responsive, feedback, and HTTP utilities belong in `src/shared`.
- Repo assets and references live in `docs/`, `data/`, and `artifacts/`. Local SQLite database: `data/astererp.db`.

### 调试数据库定位约定（防误读）

- 应用级 SQLite 受管理数据库统一落盘目录：`backend/AsterERP.Api/data/application-databases/{tenantId}/{appCode}/{databaseName}`。
- 本次调试固定数据库文件路径（不得猜测、不得使用其他同名目录）：`D:\Code\AsterERP\backend\AsterERP.Api\data\application-databases\tenant-a\MES\mes11.db`。
- 调试或复现应用数据问题时，优先以绝对路径读取上述文件，并在排查记录中明确标注该来源路径。

## Commands
- Backend build: `dotnet build AsterERP.sln`
- Backend run: `dotnet run --project backend/AsterERP.Api/AsterERP.Api.csproj`
- Frontend install: run `npm install` in `frontend/AsterERP.Web`
- Frontend dev: run `npm run dev` in `frontend/AsterERP.Web`
- Frontend build: run `npm run build` in `frontend/AsterERP.Web`

Backend tests live in `backend/AsterERP.Api.Tests`. Verify changes with `dotnet build AsterERP.sln`, `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj`, plus affected API/UI smoke checks.

## Coding Rules
- TypeScript/React uses 2-space indentation. C# uses standard formatting with nullable reference types enabled.
- C# types and members use `PascalCase`; local variables, hooks, and utilities use `camelCase`.
- Keep each core class in its own file. Do not mix unrelated responsibilities in one file or one large method.
- Prefer small focused functions. Split methods that combine validation, mapping, branching, I/O, persistence, or error handling.
- Do not add bridge/shim/facade layers that carry business logic. Put logic in the existing backend or frontend layer that owns it.

## Backend Rules
- Use async-first APIs for endpoints, services, repositories, and I/O-bound infrastructure.
- Endpoint handlers must accept `CancellationToken cancellationToken` and pass it through the full downstream chain.
- Database, file, HTTP, and cache operations must be cancellable. Do not hide blocking I/O behind sync helpers.
- Keep endpoints thin. Do not put business decisions, permissions, transactions, or persistence logic in handlers.
- Standard single-table CRUD should use `CrudAppServiceBase<TEntity, TListDto, TUpsertRequest>` and `MapCrudEndpoints`.
- Use atomic APIs directly for multi-table joins, custom transactions, or non-CRUD flows.
- Business list/detail/export queries that are scoped by organization, department, owner, tenant, role, or other data authority must use ORM data filters/global query filters.
- Data-permission filtering is forbidden in controllers, application services, repositories, frontend state, request parameters, or ad hoc `Where` composition that is not registered through the ORM data-filter mechanism.
- Data-permission filters must be database-side predicates produced by the ORM filter and must preserve pagination, sorting, projection, detail lookup, batch operations, and export boundaries.
- If the current ORM data-filter capability is not implemented for the required scope, implement the ORM data-filter capability first; do not deliver the menu feature with temporary service-layer or repository-layer filtering.

## Frontend Rules
- Reuse shared primitives such as `ResponsivePage`, `AdaptiveSearchForm`, `ResponsiveToolbar`, `ModalForm`, `DataTable`, `PermissionButton`, `useMessage`, and `useConfirm`.
- New standard list CRUD pages should use `CrudPage` and `useCrudResource` from `src/shared/components/crud-page`.
- Use atomic shared components directly only when the page needs master-detail, multiple modals, or custom workflows.
- Do not duplicate table, form, permission, dictionary, loading-state, or request logic inside individual pages.
- Do not put backend business rules, permission decisions, or data consistency logic in React components.
- Do not use `window.alert` or `window.confirm`; use shared feedback hooks.

## Performance Rules
- List/query endpoints must be paginated or intentionally bounded.
- Use database-side filtering, sorting, grouping, aggregation, and projection. Avoid loading full entities or filtering large data in memory.
- Use no-tracking read queries where tracking is unnecessary.
- Avoid N+1 queries, lazy-loading surprises, per-row `SaveChanges`, and database/external-service calls inside loops.
- Batch imports and writes where practical. Stream or chunk large files/exports instead of buffering them fully in memory.
- Frontend pages must avoid over-fetching, render only visible/paginated data, debounce search where appropriate, and reuse React Query/shared request hooks.
- API contracts should be shaped for the actual UI use case, return lightweight DTOs, and enforce backend limits regardless of frontend validation.

## Validation Rules
- Backend changes require `dotnet build AsterERP.sln` and smoke checks for affected API paths.
- Frontend changes require `npm run build` and smoke checks for affected pages.
- Full-stack changes require verifying the route/state/API/controller/service/repository/database response chain.
- Backend runtime verification must use a restarted API process when backend code changed.
- Do not use anonymous `401` responses as proof of business behavior.
- E2E failures require checking `error-context.md` before deciding root cause.

## Menu Feature Mandatory RBAC Rule
- Any new menu feature is mandatory RBAC-bound and must not be delivered without all items below:
- Define menu-level permission code (view/list) and action-level permission codes (add/edit/delete/export etc.) before implementation.
- Backend endpoints for the feature must apply `Permission` attributes and return `ApiResult` with trace context.
- Backend data access for the feature must evaluate whether data-permission filtering is required. If required, it must be implemented through ORM data filters/global query filters before the feature is delivered; if not required, record the reason in the delivery notes.
- Frontend page entry and action controls must be permission-gated and not rely on UI visibility alone; backend authorization must still be enforced.
- The change must be added to `docs/architecture-and-tech-framework.md` under the "菜单功能清单" and reviewed for RBAC status.
- The change must include end-to-end chain verification: Route -> Page -> API -> Service -> ORM data filter -> DB and permission deny path.

## Quality Checklist
Before shipping, confirm:
- Responsibilities and dependencies still match the backend/frontend layer boundaries.
- Async and `CancellationToken` flow through changed backend request paths.
- Queries are bounded, projected, and free of obvious N+1 or per-row write patterns.
- Shared frontend components/hooks are reused instead of duplicated.
- API DTOs are small, stable, and aligned with the current UI.
- Changed behavior was built and smoke-tested, or the remaining blocker is explicitly documented.

## Tests, Commits, and Security
- Backend tests should go in a dedicated `*Tests.cs` project. Frontend tests should be `*.test.ts` or `*.test.tsx`.
- Commit messages should be short, imperative, and focused on one change.
- PRs should include summary, affected areas, linked issues when available, screenshots for UI changes, and verification steps.
- Do not commit secrets. Use `backend/AsterERP.Api/appsettings*.json` and `frontend/AsterERP.Web/.env.*` for environment-specific settings.
- Backend CORS defaults expect Vite on `http://localhost:5173`.

## Git Push Credentials

- Code push for this repository must use the SSH credential `id_ed25519_git`.
- Ensure the key is loaded in the local SSH agent before pushing (for example `ssh-add`).
- Do not place private key material in repository files.

## Git Maintenance And Disk Cleanup

- When `D:\Code\AsterERP\.git` grows abnormally, first run `git count-objects -vH` and `git fsck --connectivity-only` to distinguish reachable objects from garbage.
- Git garbage reported by `git count-objects -vH`, including unreachable/dangling objects and `.git/objects/**/tmp_obj_*`, is approved for cleanup when connectivity checks do not report broken links.
- Use this cleanup order under disk pressure: `git reflog expire --expire=now --expire-unreachable=now --all`, then `git prune --expire=now`, then remove any remaining reported `tmp_obj_*` files by exact path, then run `git gc --prune=now` after enough free space is available.
- Do not delete `.git` or reachable object directories manually. Prefer Git maintenance commands and exact garbage paths only.

开发时要注意使用ABP架构
