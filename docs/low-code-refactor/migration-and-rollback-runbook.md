# One-time migration, maintenance lock, and rollback runbook

This runbook describes the auditable deployment chain for the latest-only Low-Code Studio. It does not enable a second runtime, dual writes, a compatibility route, or a long-lived feature flag.

## Required evidence

Before changing any persisted document, create an evidence record that conforms to `migration-evidence.schema.json`. Every operation is explicitly `Pass`, `Fail`, or `Blocked`; an unavailable credential, provider container, maintenance window, or authorized smoke test is `Blocked`, never a fabricated success.

Required identifiers are `migrationId`, `maintenanceLockId`, `sourceCommit`, `targetCommit`, `previousArtifactId`, `publishedArtifactId`, `healthCheckId`, `operator`, and `traceId`. The record also stores the backup path and SHA-256, rollback revision/artifact pointers, failure reason, and retry condition.

## Fixed database and baseline

The approved debug database is `D:\Code\AsterERP\backend\AsterERP.Api\data\application-databases\tenant-a\MES\mes11.db`. Capture the source commit, dependency lockfiles, database schema/seed snapshot, provider capability matrix, and artifact pointer before opening the maintenance window.

```powershell
pwsh -File docs/low-code-refactor/phase0-control.ps1 -Action CaptureBaseline
pwsh -File docs/low-code-refactor/phase0-performance.ps1 -Action ValidatePlan
pwsh -File docs/low-code-refactor/phase0-control.ps1 -Action CaptureSnapshot -WhatIf:$false
```

The snapshot is read-only and must include its byte count and SHA-256. A missing snapshot, maintenance lock, previous artifact pointer, or authorized operator is `Blocked`.

## Maintenance window and deployment

1. Freeze document writes, record the maintenance lock, migration ID, operator, tenant/application scope, and trace ID.
2. Stop active migration jobs and confirm there is no in-flight deployment.
3. Verify the backup hash and the previous published artifact/revision pointer.
4. Run schema migration, document migration, canonical validation, hash generation, compiler validation, manifest/security diagnostics, and Golden Case regression.
5. Publish the immutable artifact transactionally with its revision, manifest, compiler metadata, hash, signature, and audit record.
6. Restart the API process. Health checks must run against the restarted process.
7. With an authorized user, test Designer read/write, publish, Runtime load, Data Studio read/write permissions, tenant isolation, audit records, and the deny paths. Anonymous `401` is not business evidence.
8. Mark the evidence `Pass` only when every check has an actual trace. Otherwise preserve `Blocked` or `Fail` and do not release the lock.

## Health check command

```powershell
pwsh -File docs/low-code-refactor/phase0-control.ps1 -Action HealthCheck `
  -HealthUri http://127.0.0.1:5000/api/health `
  -HealthCheckId HC-<migrationId>
```

The response must be HTTP 2xx and the evidence must include the response hash, process restart time, database connection result, tenant boundary result, permission registration result, artifact integrity result, and an authorized business smoke trace.

## Rollback triggers and order

Rollback on schema/compile/manifest failure, transaction boundary failure, health failure, permission or tenant leakage, artifact hash mismatch, unauthorized secret exposure, or failed authorized smoke test.

1. Stop writes and retain the failed evidence and logs.
2. Verify `backupSha256`, then restore only the approved database snapshot while holding the maintenance lock.
3. Restore the published pointer to `previousArtifactId` and the document revision pointer to `rollbackRevisionId`; do not re-enable old routes or runtime code.
4. Restart the API and repeat authorized health, permission-deny, tenant-isolation, artifact, and business smoke checks.
5. Keep the system `Blocked` until all checks pass and the evidence record is updated.

```powershell
pwsh -File docs/low-code-refactor/phase0-control.ps1 -Action RestoreSnapshot `
  -SnapshotPath artifacts/phase0/snapshots/<timestamp>-mes11.db `
  -MaintenanceLockId LOCK-<id> -AllowRestore
pwsh -File docs/low-code-refactor/phase0-control.ps1 -Action ValidateEvidence
```

`-AllowRestore` is mandatory and is not a substitute for the maintenance lock or operator authorization. Artifact and revision pointers are restored by the release system from the evidence record; the script only restores the approved snapshot file.

## Evidence status

- `Pass`: implementation, real tests, authorized API/UI smoke, audit chain, and rollback evidence all exist.
- `Fail`: a required business, security, consistency, or rollback condition failed; preserve evidence and execute rollback.
- `Blocked`: an external dependency or required authorization is unavailable; record the exact recovery condition and do not close the task.
