# HAO-97 publish pipeline evidence

## Current chain

The ABP Application Service publish path is now:

`PublishPageAsync -> transaction -> PrepareForPublishAsync ->
canonical/validated DesignerDocument -> CompileSchema -> artifact/document
parity -> hash/signature/manifest validation -> artifact publish -> published
schema/menu/permission/page/version writes`.

`PrepareForPublishAsync` performs a single-page migration/normalization inside
the caller transaction. It reuses an unchanged canonical document without a
new revision or migration record; a changed source updates the document,
creates the next revision, and records the previous revision as rollback
pointer.

The compiler no longer consumes the page draft directly for publish. The
compiled artifact document is canonicalized and its document hash must equal
the persisted DesignerDocument hash before any published pointer is written.
Published schema and menu upserts short-circuit when their current canonical
content is unchanged, preventing repeat-publish VersionNo/time churn.

The source collection boundary is also latest-only: `ApplicationPublishSourceCollector`
accepts only the explicit `Full`/`Trimmed` publish modes and validated repository
relative paths. `runtimeRegistry.mes`, `runtimeRegistry.wms`, and
`runtimeRegistry.empty` are rejected as invalid source inputs; published runtime
behavior comes from the signed `RuntimeArtifact` plus its manifest metadata, not
from a copied registry file.

## Automated evidence

- `ApplicationDevelopmentMigrationTests`: 6/6 passed, including unchanged
  prepare idempotency, source-change revision creation, and rollback pointer.
- `ApplicationDevelopmentDesignerModeContractTests`: latest `structured`
  is the only accepted designer semantic; `FullDesigner`, `BusinessObject`,
  `v3`, and `v4` are rejected.
- `ApplicationPublishSourceCollectorTests`: missing files, legacy registries,
  traversal/absolute paths, and unknown publish modes fail closed; the
  duplicate artifact publish case remains idempotent.
- Full backend regression after the change: 587 tests passed in the current
  debug validation run; Release validation remains required after the final
  commit.

## Remaining acceptance boundary

Failure injection after each external write, authorized publish/API smoke,
production maintenance window, and rollback rehearsal still require the
real release environment. They remain Blocked rather than simulated.
