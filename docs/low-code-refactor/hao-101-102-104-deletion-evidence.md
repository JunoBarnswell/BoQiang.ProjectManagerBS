# HAO-101 / HAO-102 / HAO-104 Latest-only deletion evidence

This record audits the current branch against the latest-only replacement
contract. It does not treat an absent string as proof of production readiness;
each deletion is tied to a commit, a call-chain decision, a guard, and the
remaining operational evidence.

## Scope and decision

The audited product chain is the Low-Code Studio designer, the published
runtime artifact path, the runtime kernel, the designer compiler/validator,
and the low-code editor migration inputs. Unrelated workflow, Flowise, Aster
Scene, and platform schema compatibility code is outside this scope.

The current decision is **In Review / external evidence Blocked**:

- latest-only source, artifact-entry, module-map, and dependency audit guards
  pass with no production `full-designer` dependency;
- old generic runtime entrypoints and registries have been deleted;
- migration parsers remain only as one-time persisted-data inputs and are not
  imported by the runtime chain;
- maintenance-window, real-provider, browser, and rollback evidence is not
  available locally and is not simulated.

The previous P0 production-coupling blocker was removed by deleting the unused
`PublishSidebarPanel.tsx` after the latest Page Studio host became the sole
publish surface. External maintenance-window, provider, browser, and rollback
evidence remain open and are not simulated.

## Deletion ledger

| Obsolete surface | Evidence | Deletion reason | Guard |
| --- | --- | --- | --- |
| `shared/runtime/PageRenderer.tsx` | `bec698a97` removed it; no production import remains | A generic renderer could bypass the signed `RuntimeArtifact` boundary | `RuntimeLatestOnlyGuardTests`, `LatestOnlyDeletionAcceptanceTests` |
| `core/ui-engine/ActionRegistry.ts`, `ComponentRegistry.ts`, `SchemaValidator.ts` | `bec698a97` removed them after the old renderer consumer was deleted | The current runtime uses manifest/action handler contracts in `runtime-kernel` | `RuntimeLatestOnlyGuardTests`, `LatestOnlyDeletionAcceptanceTests` |
| `shared/runtime/designer-document/DesignerRuntimeRenderer.tsx` | `8a075ae17` removed the legacy renderer implementation | Design preview and formal runtime now share the current component runtime host | `LatestOnlySourceScanGuardTests`, `LatestOnlyDeletionAcceptanceTests` |
| `runtimeDocumentCodec.ts` | `c00f1c08e` removed the obsolete runtime codec | Runtime accepts compiled artifact data; document migration belongs to the editor/migration boundary | `LatestOnlyDeletionAcceptanceTests` |
| orphaned runtime tables, bindings, types, and scheduler modules | `695f24c81` removed the unused modules | Prevent a second runtime data model from being reintroduced | `LatestOnlyDeletionAcceptanceTests` |
| numeric/v3/v4 formal contract routing | `f3226f825` replaced numeric contract routing; formal latest schema is checked by the source guard | Version-number routing would create a second semantic contract | `LatestOnlySourceScanGuardTests` |

## Current chain invariants

1. `RuntimePage.tsx` builds one signed artifact shape and renders through
   `ComponentRuntimeHost`.
2. `RuntimeKernel.ts` verifies artifact integrity, manifest types, actions,
   scopes, bindings, and tree invariants before rendering.
3. `ApplicationDevelopmentSchemaCompiler.cs` emits the single
   `designerDocument` renderer plus signature metadata.
4. `RuntimePageSchemaService.cs` is the backend runtime schema boundary; it
   does not select a legacy renderer.
5. Runtime-chain files do not import or call
   `parseLegacyDesignerExpressionString`, `inferLegacyOperation`, or the
   document migration service.

These invariants are executable in
`backend/AsterERP.Api.Tests/LatestOnlyDeletionAcceptanceTests.cs` and
`LatestOnlyArchitectureAuditTests.cs`.

They do not replace the required external runtime and release evidence.

## Deliberately retained migration inputs

The following are not runtime fallback paths and therefore are not deleted in
this audit:

- `designerDocumentCodec.ts`: rejects numeric schema versions and normalizes
  persisted input before producing the latest document shape.
- `designerExpressionModel.ts`: parses old persisted expression text only at
  the editor/migration boundary.
- `designerActionModel.ts`: normalizes old persisted action forms into the
  current action graph.
- `CurrentDocumentMigration.ts` and
  `ApplicationDesignerDocumentMigrationService.cs`: perform the one-time
  document migration and remove editor-only state.
- `ApplicationDevelopmentSchemaValidator.cs`: remains the current validation
  boundary used by document store, migration, compiler, and runtime schema
  service; it is not an obsolete parser.

Deleting these files without first migrating every stored document would lose
existing persisted data. Their presence is explicitly quarantined by the
runtime-chain dependency test and must not be interpreted as a second runtime.

## HAO-104 audit findings not safely changeable in Worker A

| Finding | Current evidence | Decision |
| --- | --- | --- |
| Native HTML5 DnD remains in resource binding and table-column auxiliary panels | `ResourceExplorer.tsx`, `VariableResourcePanel.tsx`, `TableColumnWorkbench.tsx`, and visual table editors contain `draggable`/`onDragStart`/`onDrop` | **Blocked**: replacing these interactions requires frontend production changes outside the allowed Worker A paths; no false Pass is recorded |
| Textareas remain for SQL/configuration fields | `configSchemas/queryDatasetSchemas.ts`, connection-test schemas, and integration/API configuration schemas expose controlled SQL/configuration text fields | **Blocked / scope clarification required**: these are Data Studio configuration surfaces, not the deleted runtime renderer; removal requires Data Studio product changes outside this worker |
| View DDL contains provider `CREATE VIEW`/`DROP VIEW` statements | Provider classes generate quoted, controlled DDL and the view deployment service owns the plan/transaction boundary | **Pass for deletion audit**: this is provider-specific latest implementation, not the old UI drop/create shortcut; real four-provider execution evidence remains external Blocked |
| `publishedSchemaVersionNo` and `targetSchemaVersionNo` remain in page/action DTOs | They are active persisted/public fields used by page and action APIs | **Blocked**: removing them requires a coordinated database/public-contract migration and is outside the permitted files |

## External evidence still required for HAO-101/102

The following cannot be proven by repository guards or local unit tests:

- authorized maintenance lock, read-only backup hash, operator, health-check
  trace, publish pointer, and rollback rehearsal;
- authorized SQL Server, MySQL, PostgreSQL, and SQLite integration traces;
- restarted authorized API/UI traces for permissions, tenant boundaries,
  artifact tamper rejection, audit chain, browser compatibility, and
  responsive behavior;
- repeated Release performance/memory evidence for the full acceptance matrix;
- final release-commit deletion scan after all external migration evidence is
  attached.

No anonymous `401`, mock provider, placeholder artifact, or shadow runtime is
accepted as a substitute for these records.
