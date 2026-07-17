# AsterScene GA Acceptance Record

Date: 2026-06-19

## AS-02 PublicRead / Workspace data boundary

The AsterScene data-permission pipeline now classifies public reads separately from
private workspace requests. `GET /api/public/asterscene/*` and authenticated
community actions under `/api/community/asterscene/*` use the database-side
PublicRead predicates: a public work must be non-deleted, `Published`, and
non-`Private`; a publish version must be non-deleted, `Active`, and non-`Private`;
creator profiles must be active. Private `/api/asterscene/*`, usage, and admin
routes retain the current `TenantId + AppCode` workspace boundary and owner
restriction for creator-owned public records. Community reaction, remix, and
report replay rows are additionally scoped to the current user in the PublicRead
request scope.

The Work page gates Like, Favorite, Report, and Remix with the same backend
permission codes (`asterscene:community:interact` and
`asterscene:remix:create`), so an unauthorized user cannot invoke a visible
frontend action and the backend remains the final authorization boundary.

## Pass

| Case | Evidence |
| --- | --- |
| Contract naming gate | `rg` found no AsterScene-owned retired document/manifest suffix names or self-owned version markers under backend/frontend/docs. |
| Legacy route/code gate | `rg` found no runnable retired route/code tokens under backend/frontend/docs; sampled retired API paths returned 404. |
| DB cleanup gate | `visual_*`, retired `as_*`, retired permissions, retired role permissions, and retired menus all count 0 in `backend/AsterERP.Api/data/astererp.db`. |
| Public identifiers | `ux_as_publish_code`, `ux_as_public_slug`, and `ux_as_creator_handle` are global unique indexes without tenant/app columns. |
| Assets chunk upload | Authenticated SYSTEM smoke created a real project asset through start-upload, checksum chunk upload, complete; returned `Ready` and checksum matched. |
| Browser assets page | In-app browser login `admin/admin123` -> SYSTEM -> `/assets`; `Chunk upload` file input and uploaded smoke asset were visible, no retired text. |
| Support lifecycle | Authenticated smoke created support ticket, read detail with diagnostics, added comment, closed ticket, and repeated close idempotently without duplicate close comments. |
| Backend build/tests | `dotnet build AsterERP.sln` passed; `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj` passed 59 tests. |
| Frontend gates | `npm run typecheck`, `npm run lint`, `npm run test -- --run`, and `npm run build` passed. |

## Blocked Or Not Fully Proven

## AS-08 Governance persistence boundary (current implementation)

The backend now persists moderation decisions, evidence, appeals, and appeal
decisions as append-only history rows. Moderation transitions are explicit:
`Open -> Allowed|Removed`, `Removed -> Restored`; invalid transitions return
`StateChangeNotAllowed`. Report, decision, appeal, and appeal-decision requests
carry mutation identifiers and replay existing results without creating another
history row. Approved appeals restore the public work in the same transaction.

Support administration has tenant/app-scoped list/detail, admin comments, and
open/closed status transitions with auditable comment rows. The existing
owner-facing support endpoints retain explicit owner checks.

This commit does not claim the AS-08 frontend Admin surface is complete. The
current page still exposes only the existing moderation Allow/Remove controls;
appeal and support-admin queues require a follow-up frontend implementation and
browser verification.

| Case | Status | Reason |
| --- | --- | --- |
| Payment provider lifecycle | Blocked | No live payment provider credentials or webhook endpoint were provided for success/failure/cancel/expire verification. |
| AI provider success path | Blocked | Current implementation safely fails/refunds when provider is not configured; provider-backed success/apply needs configured provider credentials. |
| Full B1/B2/B3 performance lab | Blocked | Requires controlled 4G/mobile/desktop profiling environment and repeatable asset corpus; current evidence covers build size warnings and runtime smoke only. |
| Full GA E2E matrix | In progress | Create/save/upload/publish/public/community/support subsets have evidence; payment, AI success, and full governance appeal/restore still need dedicated end-to-end runs. |

## Known Warnings

| Warning | Status |
| --- | --- |
| NU1902 MailKit/MimeKit | Existing dependency vulnerability warnings remain during backend build/test. |
| Vite chunk size | `aster-scene-engine` and `workflow-bpmn` chunks exceed 500 kB warning threshold. |
