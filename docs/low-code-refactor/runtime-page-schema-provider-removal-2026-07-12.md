# Legacy page-schema provider removal

## Root cause

The Runtime path now reads published `ApplicationDesignerRuntimeArtifactEntity` records through `ApplicationDevelopmentPageEntity.PublishedArtifactId`. The retired `system_page_schemas` table is therefore not a Runtime source. Keeping its ORM entity available allowed application code and data-permission registration to accidentally reintroduce the legacy source.

## Completed boundary

- RuntimeCore no longer creates, registers, or filters `system_page_schemas`.
- `SystemPageSchemaEntity` has been removed from the formal Runtime module.
- The legacy migration reads the old table through a migration-only raw row projection (`LegacyPageSchemaRow`) and never exposes it to Runtime/Application services.
- A successful migration converts published legacy rows to DesignerDocument revisions and Runtime artifacts, then drops the legacy table.
- The migration owns one database transaction when no outer transaction exists; conversion, pointer updates, and table deletion commit together, and any conversion failure rolls back all new rows while retaining the source table for retry.

## Verification

```text
dotnet build AsterERP.sln --no-restore
dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ApplicationDevelopmentMigrationTests|FullyQualifiedName~ApplicationDatabaseBaselineSeederTests" -m:1
```

The migration tests cover the no-table no-op, raw-row conversion and table deletion, and rollback on invalid legacy JSON. The formal backend source has no `SystemPageSchemaEntity` reference; source-scan assertion strings in tests are intentional guards.
