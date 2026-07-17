# Workspace filter isolation evidence

## Root cause

`/api/application-development-center/*` was not classified as a workspace request. The data-permission middleware therefore selected the main database and did not register the Application Development workspace filters for designer pages, documents, revisions, migration runs, and runtime artifacts.

Published Data Center snapshots were also absent from the workspace filter registry, so a latest-snapshot lookup had no ORM-enforced tenant/app boundary.

## Implemented boundary

- `DataPermissionRequestClassifier` classifies the Development Center route as a workspace API.
- `DataPermissionFilterRegistrar` selects the workspace database before clearing/registering filters, registers descriptor filters before module workspace filters, and registers `ApplicationDataCenterPublishedSnapshot` as a tenant/app workspace entity.
- `ApplicationDesignerDocumentMigrationService` resolves the authenticated tenant/app workspace, uses its current application database, and limits migration candidates to that workspace. Unauthenticated startup seed execution keeps its existing all-workspace behavior.
- `ApplicationDataCenterPublishedSnapshotService.GetLatestAsync` resolves the current workspace and relies on the registered ORM filter for the database-side boundary.
- `RuntimeGridViewService` uses `RequireApplicationDbAsync` for all grid-view reads and writes.

## Verification

- `dotnet build AsterERP.sln --no-restore` — PASS.
- Targeted tests for development-center classification, migration scope, and Data Center permission architecture — PASS (15/15).
- Cross-workspace migration test seeds tenant-a/MES, tenant-b/MES, and tenant-a/WMS pages under a tenant-a/MES identity; only tenant-a/MES is migrated — PASS.

No frontend, Contracts, Data Studio DDL/View, or user database files were changed by this task.
