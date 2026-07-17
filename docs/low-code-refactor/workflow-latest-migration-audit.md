# Workflow latest contract migration audit

## Canonical records

- `WorkflowBusinessModelLatest` is persisted as a versioned `workflow_model_extensions.ExtensionJson` envelope. Its `businessDesign` is the sole editable authority; BPMN is the generated deployment artifact.
- `WorkflowBindingLatestCallbackConfig` is persisted only in `workflow_bindings.BindingConfigJson` with `version: "latest"`. `StatusField` is not read to synthesize callbacks.

## Migration and failure policy

- Unversioned, malformed, or structurally invalid model/callback JSON is reported as `MigrationBlocked`; it is never silently defaulted, dual-written, or executed.
- The UI exposes the blocked model as an alert and prevents save/publish from overwriting it. The API independently checks the same envelope on save, validation, and publish.
- Existing records must be transformed by an approved deployment migration and audited before changing the status from `MigrationBlocked`; this repository does not claim a production migration was performed.

## Regression evidence

- `WorkflowBusinessModelLatestValidatorTests` validates canonical acceptance and legacy rejection.
- `WorkflowCallbackExecutorTests` verifies corrupt/unversioned callback configurations and rejects outdated empty configurations.
