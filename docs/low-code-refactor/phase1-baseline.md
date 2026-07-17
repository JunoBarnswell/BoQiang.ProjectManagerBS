# Phase 1 latest DesignerDocument baseline

## Scope

Phase 1 treats `DesignerDocument` as the only editable document source. `DesignerEditorSession` remains a separate in-memory projection, and `RuntimeArtifact` is produced only by the latest compiler after validation and signing.

## Verified boundaries

- `DesignerDocumentCodec` rejects editor-session fields, numeric schema-version negotiation, and legacy node-level `dataBinding`.
- `DesignerDocumentStore` and both document serializers reject session state before it can be persisted or hashed.
- `DesignerDocumentHash` is the canonical JSON/hash implementation used by the document codec; the top-level `documentHash` is excluded from the hash and nested values remain part of the payload.
- `CurrentDocumentMigration` accepts one-time legacy input only, rejects latest `DesignerDocument` re-entry, separates selection/viewport into `DesignerEditorSession`, preserves document permissions in its draft projection, and emits no artifact hash or signature.
- `DesignerCommandBus` records an inverse for every committed change, including custom commands that omit an explicit inverse, and supports undo/redo for those changes.

## Cases

| Case | Expected result |
| --- | --- |
| Session fields in document input | Rejected before parse, serialization, Store commit, or hash serialization |
| Legacy `dataBinding` in latest node input | Rejected; only the one-time migration may translate it |
| Latest document passed to migration | Rejected with a one-time migration boundary error |
| Invalid selected node or out-of-range viewport in legacy input | Selection is cleared and viewport is clamped to session bounds |
| Custom changed command without inverse | Snapshot inverse is recorded; undo and redo both restore the canonical document |
| Backend validator accepts `editorSession`/`dataBinding` | Blocked pending backend contract-owner change; see `BLOCKERS.md` |

## Verification commands

```powershell
npm run test -- --run src/pages/application-console/development-center/low-code-studio/document/DesignerDocumentCodec.test.ts src/pages/application-console/development-center/low-code-studio/document/DesignerDocumentHash.test.ts src/pages/application-console/development-center/low-code-studio/document/DesignerDocumentStore.test.ts src/pages/application-console/development-center/low-code-studio/commands/DesignerCommandBus.test.ts src/pages/application-console/development-center/low-code-studio/migration/CurrentDocumentMigration.test.ts
npx eslint src/pages/application-console/development-center/low-code-studio/document src/pages/application-console/development-center/low-code-studio/commands src/pages/application-console/development-center/low-code-studio/migration --max-warnings 0
```

The full frontend typecheck remains workspace-wide and must be reported separately if unrelated canvas/binding/inspector files fail.
