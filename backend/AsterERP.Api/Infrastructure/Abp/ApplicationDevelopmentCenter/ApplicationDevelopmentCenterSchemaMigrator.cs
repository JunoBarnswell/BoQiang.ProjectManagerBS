using System.Data;
using System.Text.Json;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;

/// <summary>
/// Owns the boundary between normal application startup and an explicitly invoked
/// Designer historical-schema retirement deployment.
/// </summary>
public sealed class ApplicationDevelopmentCenterSchemaMigrator(
    ApplicationDesignerDocumentMigrationService? documentMigrationService = null,
    ApplicationLegacyPageSchemaMigrationService? legacyPageSchemaMigrationService = null,
    ApplicationDesignerMigrationRunService? migrationRunService = null)
{
    private const string MigrationWatermarkKey = "application-designer-historical-retirement";
    private const string SourceSchemaFingerprint = "historical-page-schema-v1";
    private const string TargetSchemaFingerprint = "designer-document-latest-v1";

    private static readonly Type[] EntityTypes =
    [
        typeof(ApplicationDevelopmentVersionEntity),
        typeof(ApplicationDevelopmentModuleEntity),
        typeof(ApplicationDevelopmentPageEntity),
        typeof(ApplicationSharedResourceEntity),
        typeof(ApplicationBusinessObjectDesignEntity),
        typeof(ApplicationDesignerDocumentEntity),
        typeof(ApplicationDesignerRevisionEntity),
        typeof(ApplicationDesignerMigrationEntity),
        typeof(ApplicationDesignerRuntimeArtifactEntity),
        typeof(ApplicationDesignerEditorSessionEntity),
        typeof(ApplicationDesignerPublishRecordEntity),
        typeof(ApplicationDesignerMigrationRunEntity),
        typeof(ApplicationDesignerMigrationWatermarkEntity),
        typeof(ApplicationMonitoringEventEntity)
    ];

    /// <summary>
    /// Compatibility name for existing maintenance callers. It is an explicit
    /// deployment migration entrypoint and must never be called from startup.
    /// </summary>
    public Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken) =>
        RunDeploymentMigrationAsync(db, cancellationToken);

    /// <summary>
    /// Startup path: only ensure latest owned objects and validate the committed
    /// retirement boundary. It does not inspect, read, rename, migrate, or drop
    /// historical schema.
    /// </summary>
    public async Task EnsureCurrentSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schema = new SqliteSchemaExecutor(db);
        await ValidateLatestSchemaAsync(schema, cancellationToken);
        await ValidateRetirementWatermarkAsync(db, cancellationToken);
    }

    /// <summary>
    /// Deployment-only path. It records an immutable backup manifest and lock,
    /// applies the one-time retirement procedure, validates the resulting
    /// latest contract, and only then commits the retirement watermark.
    /// </summary>
    public async Task RunDeploymentMigrationAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (documentMigrationService is null)
        {
            throw new InvalidOperationException("Application Designer deployment migration requires ApplicationDesignerDocumentMigrationService.");
        }

        db.CodeFirst.InitTables(EntityTypes);
        var schema = new SqliteSchemaExecutor(db);
        var inventory = await InventoryHistoricalSchemaAsync(schema, cancellationToken);
        var backupManifest = JsonSerializer.Serialize(new
        {
            kind = "application-designer-historical-retirement",
            sourceSchemaFingerprint = SourceSchemaFingerprint,
            targetSchemaFingerprint = TargetSchemaFingerprint,
            inventory
        });
        var runs = migrationRunService ?? new ApplicationDesignerMigrationRunService();
        var run = await runs.AcquireAsync(
            db,
            "*",
            "*",
            MigrationWatermarkKey,
            backupManifest,
            SourceSchemaFingerprint,
            TargetSchemaFingerprint,
            "deployment-migration",
            null,
            cancellationToken);

        try
        {
            await MarkRetirementPendingAsync(db, backupManifest, cancellationToken);
            await documentMigrationService.MigrateAsync(cancellationToken);
            if (legacyPageSchemaMigrationService is not null)
            {
                await legacyPageSchemaMigrationService.MigrateAsync(cancellationToken);
            }

            await RetireHistoricalSchemaAsync(db, schema, cancellationToken);
            await EnsureLatestSchemaObjectsAsync(schema, validatePublishData: true, cancellationToken);
            await ValidateHistoricalSchemaRetiredAsync(schema, cancellationToken);
            await RecordRetirementWatermarkAsync(db, backupManifest, cancellationToken);
            await ValidateRetirementWatermarkAsync(db, cancellationToken);
            await runs.CompleteAsync(db, run, $"designer-retirement:{TargetSchemaFingerprint}", "[]", cancellationToken);
        }
        catch (Exception exception)
        {
            await runs.FailAsync(db, run, exception, cancellationToken);
            throw;
        }
    }

    private static async Task RetireHistoricalSchemaAsync(
        ISqlSugarClient db,
        SqliteSchemaExecutor schema,
        CancellationToken cancellationToken)
    {
        await schema.RenameColumnIfExistsAsync("app_dev_pages", "PublishedPageSchemaId", "PublishedArtifactId", cancellationToken);
        await schema.DropColumnIfExistsAsync("app_designer_publish_records", "PublishedPageSchemaId", cancellationToken);
        await schema.DropTableIfExistsAsync("system_page_schemas", cancellationToken);

        var ownsTransaction = db.Ado.Transaction is null;
        if (ownsTransaction)
        {
            await db.Ado.BeginTranAsync();
        }

        try
        {
            await DropLegacyColumnIfExistsAsync(db, "LayoutDraftJson", cancellationToken);
            await DropLegacyColumnIfExistsAsync(db, "SchemaDraftJson", cancellationToken);
            if (ownsTransaction)
            {
                await db.Ado.CommitTranAsync();
            }
        }
        catch
        {
            if (ownsTransaction && db.Ado.Transaction is not null)
            {
                await db.Ado.RollbackTranAsync();
            }

            throw;
        }
    }

    private static async Task EnsureLatestSchemaObjectsAsync(
        SqliteSchemaExecutor schema,
        bool validatePublishData,
        CancellationToken cancellationToken)
    {
        schema.EnsureNullableColumn("app_dev_pages", "ParentPageId", "TEXT NULL");
        schema.EnsureColumn("app_designer_revisions", "CompilerRevision", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_designer_revisions", "ManifestHash", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_designer_revisions", "ManifestJson", "TEXT NOT NULL DEFAULT '{}' ");
        schema.EnsureColumn("app_designer_revisions", "SourceArtifactHash", "TEXT NULL");
        schema.EnsureColumn("app_designer_runtime_artifacts", "MigrationRevision", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureNullableColumn("app_designer_runtime_artifacts", "SourceArtifactId", "TEXT NULL");
        schema.EnsureNullableColumn("app_designer_runtime_artifacts", "SourceArtifactHash", "TEXT NULL");
        schema.EnsureColumn("app_designer_runtime_artifacts", "SourceArtifactJson", "TEXT NULL");
        schema.EnsureColumn("app_designer_publish_records", "RevisionNumber", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("app_designer_publish_records", "DocumentHash", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_designer_publish_records", "ArtifactHash", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_designer_publish_records", "CompilerRevision", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_designer_publish_records", "ManifestHash", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_designer_publish_records", "ManifestJson", "TEXT NOT NULL DEFAULT '{}' ");
        schema.EnsureColumn("app_designer_publish_records", "MigrationRevision", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureNullableColumn("app_designer_publish_records", "SourceArtifactId", "TEXT NULL");
        schema.EnsureNullableColumn("app_designer_publish_records", "SourceArtifactHash", "TEXT NULL");
        schema.EnsureColumn("app_designer_publish_records", "OperationType", "TEXT NOT NULL DEFAULT 'Publish'");
        schema.EnsureNullableColumn("app_designer_publish_records", "OperationId", "TEXT NULL");
        schema.EnsureNullableColumn("app_designer_publish_records", "TargetArtifactId", "TEXT NULL");
        schema.EnsureNullableColumn("app_designer_publish_records", "TargetArtifactHash", "TEXT NULL");
        schema.EnsureNullableColumn("app_designer_publish_records", "OperatorUserId", "TEXT NULL");
        schema.EnsureNullableColumn("app_designer_publish_records", "TraceId", "TEXT NULL");
        schema.EnsureNullableColumn("app_designer_publish_records", "RollbackReason", "TEXT NULL");
        schema.EnsureNullableColumn("app_designer_publish_records", "FailureCode", "TEXT NULL");
        schema.EnsureNullableColumn("app_designer_publish_records", "FailureMessage", "TEXT NULL");
        schema.EnsureNullableColumn("app_designer_publish_records", "PageId", "TEXT NULL");

        if (validatePublishData)
        {
            await EnsurePublishIdempotencyDataAsync(schema, cancellationToken);
        }

        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_dev_versions_workspace ON app_dev_versions(TenantId, AppCode, Status) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_dev_modules_workspace ON app_dev_modules(TenantId, AppCode, VersionId, SortOrder) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_dev_pages_workspace ON app_dev_pages(TenantId, AppCode, VersionId, Status, SortOrder) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_dev_shared_resources_workspace ON app_dev_shared_resources(TenantId, AppCode, VersionId, Status) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_dev_pages_module ON app_dev_pages(TenantId, AppCode, ModuleId, SortOrder) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_business_object_designs_page ON app_business_object_designs(TenantId, AppCode, PageId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_business_object_designs_model ON app_business_object_designs(TenantId, AppCode, ModelCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_business_object_designs_workspace ON app_business_object_designs(TenantId, AppCode, VersionId, Status) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_designer_documents_page ON app_designer_documents(TenantId, AppCode, PageId) WHERE IsDeleted = 0;");
        await EnsureRevisionNumberUniquenessAsync(schema, cancellationToken);
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_designer_migrations_page ON app_designer_migrations(TenantId, AppCode, PageId, CreatedTime) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_designer_artifacts_document ON app_designer_runtime_artifacts(TenantId, AppCode, DocumentId, Status) WHERE IsDeleted = 0;");
        await schema.ExecuteNonQueryAsync("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_designer_artifacts_content ON app_designer_runtime_artifacts(TenantId, AppCode, DocumentId, ArtifactHash) WHERE IsDeleted = 0;", cancellationToken);
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_designer_sessions_key ON app_designer_editor_sessions(TenantId, AppCode, SessionKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_designer_publish_document ON app_designer_publish_records(TenantId, AppCode, DocumentId, CreatedTime) WHERE IsDeleted = 0;");
        await schema.ExecuteNonQueryAsync("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_designer_publish_operation ON app_designer_publish_records(TenantId, AppCode, OperationId) WHERE IsDeleted = 0 AND OperationId IS NOT NULL AND trim(OperationId) <> '';", cancellationToken);
        await schema.ExecuteNonQueryAsync("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_designer_publish_artifact ON app_designer_publish_records(TenantId, AppCode, DocumentId, ArtifactHash) WHERE IsDeleted = 0 AND OperationType = 'Publish';", cancellationToken);
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_designer_migration_run_lock ON app_designer_migration_runs(TenantId, AppCode, MigrationKey) WHERE IsDeleted = 0 AND Status = 'Running';");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_designer_migration_run_workspace ON app_designer_migration_runs(TenantId, AppCode, MigrationKey, StartedTime) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_designer_migration_watermark_key ON app_designer_migration_watermarks(TenantId, AppCode, MigrationKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_designer_migration_watermark_status ON app_designer_migration_watermarks(TenantId, AppCode, Status) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_application_monitoring_events_id ON app_application_monitoring_events(TenantId, AppCode, EventId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_app_application_monitoring_events_time ON app_application_monitoring_events(TenantId, AppCode, OccurredAt);");
    }

    /// <summary>
    /// Verifies the minimum latest-only surface through read-only SQLite metadata
    /// queries. Startup must fail closed when a deployment has not installed this
    /// surface; it must not repair it with runtime DDL.
    /// </summary>
    private static async Task ValidateLatestSchemaAsync(SqliteSchemaExecutor schema, CancellationToken cancellationToken)
    {
        var requiredTables = new[]
        {
            "app_dev_versions",
            "app_dev_modules",
            "app_dev_pages",
            "app_designer_documents",
            "app_designer_revisions",
            "app_designer_migration_runs",
            "app_designer_migration_watermarks"
        };
        foreach (var tableName in requiredTables)
        {
            if (!await schema.HasTableAsync(tableName, cancellationToken))
            {
                throw new InvalidOperationException($"Designer latest schema is unavailable: required table '{tableName}' is missing. Run the explicit deployment migration before starting the application.");
            }
        }

        var requiredColumns = new[]
        {
            ("app_dev_pages", "ParentPageId"),
            ("app_designer_documents", "CurrentRevisionId"),
            ("app_designer_revisions", "CompilerRevision"),
            ("app_designer_revisions", "ManifestHash"),
            ("app_designer_revisions", "ManifestJson"),
            ("app_designer_migration_watermarks", "MigrationKey"),
            ("app_designer_migration_watermarks", "Status"),
            ("app_designer_migration_watermarks", "SourceSchemaFingerprint"),
            ("app_designer_migration_watermarks", "TargetSchemaFingerprint")
        };
        foreach (var (tableName, columnName) in requiredColumns)
        {
            if (!await schema.HasColumnAsync(tableName, columnName, cancellationToken))
            {
                throw new InvalidOperationException($"Designer latest schema is unavailable: required column '{tableName}.{columnName}' is missing. Run the explicit deployment migration before starting the application.");
            }
        }
    }

    /// <summary>
    /// Deployment-only CAS guard. The former non-unique lookup index allowed
    /// duplicate revision numbers under a document, making concurrent saves
    /// ambiguous. Refuse dirty historical data before replacing it with the
    /// canonical unique index.
    /// </summary>
    private static async Task EnsureRevisionNumberUniquenessAsync(SqliteSchemaExecutor schema, CancellationToken cancellationToken)
    {
        var duplicates = await schema.ExecuteDataTableAsync("""
SELECT DocumentId, RevisionNumber, COUNT(1) AS DuplicateCount
FROM app_designer_revisions
WHERE IsDeleted = 0
GROUP BY TenantId, AppCode, DocumentId, RevisionNumber
HAVING COUNT(1) > 1
LIMIT 1;
""", cancellationToken);
        if (duplicates.Rows.Count > 0)
        {
            var documentId = Convert.ToString(duplicates.Rows[0]["DocumentId"]);
            var revisionNumber = Convert.ToString(duplicates.Rows[0]["RevisionNumber"]);
            throw new InvalidOperationException($"Designer deployment migration is blocked by duplicate revision '{documentId}:{revisionNumber}'. Resolve duplicates before creating the latest CAS index.");
        }

        schema.Execute("DROP INDEX IF EXISTS idx_app_designer_revisions_document;");
        await schema.ExecuteNonQueryAsync("CREATE UNIQUE INDEX IF NOT EXISTS idx_app_designer_revisions_document ON app_designer_revisions(TenantId, AppCode, DocumentId, RevisionNumber) WHERE IsDeleted = 0;", cancellationToken);
    }

    private static async Task<string> InventoryHistoricalSchemaAsync(SqliteSchemaExecutor schema, CancellationToken cancellationToken)
    {
        var inventory = new
        {
            legacyPageSchemaTable = await schema.HasTableAsync("system_page_schemas", cancellationToken),
            pageLayoutDraftColumn = await schema.HasColumnAsync("app_dev_pages", "LayoutDraftJson", cancellationToken),
            pageSchemaDraftColumn = await schema.HasColumnAsync("app_dev_pages", "SchemaDraftJson", cancellationToken),
            pagePublishedSchemaColumn = await schema.HasColumnAsync("app_dev_pages", "PublishedPageSchemaId", cancellationToken),
            publishRecordSchemaColumn = await schema.HasColumnAsync("app_designer_publish_records", "PublishedPageSchemaId", cancellationToken),
            menuSchemaColumn = await schema.HasColumnAsync("system_menus", "PageSchemaId", cancellationToken)
        };
        return JsonSerializer.Serialize(inventory);
    }

    private static async Task ValidateHistoricalSchemaRetiredAsync(SqliteSchemaExecutor schema, CancellationToken cancellationToken)
    {
        var inventory = await InventoryHistoricalSchemaAsync(schema, cancellationToken);
        using var document = JsonDocument.Parse(inventory);
        if (document.RootElement.EnumerateObject().Any(property => property.Value.GetBoolean()))
        {
            throw new InvalidOperationException($"Designer deployment migration is incomplete; historical schema remains: {inventory}");
        }
    }

    private static async Task ValidateRetirementWatermarkAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var watermark = (await db.Queryable<ApplicationDesignerMigrationWatermarkEntity>()
                .Where(item => item.TenantId == "*" && item.AppCode == "*" && item.MigrationKey == MigrationWatermarkKey && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (watermark is null ||
            !string.Equals(watermark.Status, "Retired", StringComparison.Ordinal) ||
            !string.Equals(watermark.SourceSchemaFingerprint, SourceSchemaFingerprint, StringComparison.Ordinal) ||
            !string.Equals(watermark.TargetSchemaFingerprint, TargetSchemaFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Designer latest schema is unavailable: run the explicit deployment migration before starting the application.");
        }
    }

    private static async Task RecordRetirementWatermarkAsync(ISqlSugarClient db, string manifest, CancellationToken cancellationToken)
    {
        var existing = (await db.Queryable<ApplicationDesignerMigrationWatermarkEntity>()
                .Where(item => item.TenantId == "*" && item.AppCode == "*" && item.MigrationKey == MigrationWatermarkKey && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (existing is null)
        {
            throw new InvalidOperationException("Designer deployment migration lost its retirement watermark.");
        }

        existing.Status = "Retired";
        existing.SourceSchemaFingerprint = SourceSchemaFingerprint;
        existing.TargetSchemaFingerprint = TargetSchemaFingerprint;
        existing.AppliedTime = DateTime.UtcNow;
        existing.DiagnosticsJson = manifest;
        await db.Updateable(existing).UpdateColumns(item => new
        {
            item.Status,
            item.SourceSchemaFingerprint,
            item.TargetSchemaFingerprint,
            item.AppliedTime,
            item.DiagnosticsJson
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task MarkRetirementPendingAsync(ISqlSugarClient db, string manifest, CancellationToken cancellationToken)
    {
        var existing = (await db.Queryable<ApplicationDesignerMigrationWatermarkEntity>()
                .Where(item => item.TenantId == "*" && item.AppCode == "*" && item.MigrationKey == MigrationWatermarkKey && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (existing is null)
        {
            await db.Insertable(new ApplicationDesignerMigrationWatermarkEntity
            {
                TenantId = "*",
                AppCode = "*",
                MigrationKey = MigrationWatermarkKey,
                SourceSchemaFingerprint = SourceSchemaFingerprint,
                TargetSchemaFingerprint = TargetSchemaFingerprint,
                Status = "Pending",
                AppliedTime = DateTime.UtcNow,
                DiagnosticsJson = manifest,
                CreatedBy = "deployment-migration",
                CreatedTime = DateTime.UtcNow,
                IsDeleted = false
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        existing.Status = "Pending";
        existing.DiagnosticsJson = manifest;
        await db.Updateable(existing)
            .UpdateColumns(item => new { item.Status, item.DiagnosticsJson })
            .ExecuteCommandAsync(cancellationToken);
    }

    private static async Task EnsurePublishIdempotencyDataAsync(SqliteSchemaExecutor schema, CancellationToken cancellationToken)
    {
        var missingOperationIds = await schema.ExecuteDataTableAsync(
            "SELECT TenantId, AppCode, DocumentId, ArtifactHash FROM app_designer_publish_records WHERE IsDeleted = 0 AND OperationType = 'Publish' AND (OperationId IS NULL OR trim(OperationId) = '') LIMIT 10;",
            cancellationToken);
        if (missingOperationIds.Rows.Count > 0)
        {
            throw new InvalidOperationException($"Application Designer publish migration is blocked: active Publish records are missing OperationId. Samples: {DescribeRows(missingOperationIds, "TenantId", "AppCode", "DocumentId", "ArtifactHash")}");
        }

        var duplicateArtifacts = await schema.ExecuteDataTableAsync(
            "SELECT TenantId, AppCode, DocumentId, ArtifactHash, COUNT(1) AS DuplicateCount FROM app_designer_runtime_artifacts WHERE IsDeleted = 0 GROUP BY TenantId, AppCode, DocumentId, ArtifactHash HAVING COUNT(1) > 1 LIMIT 10;",
            cancellationToken);
        if (duplicateArtifacts.Rows.Count > 0)
        {
            throw new InvalidOperationException($"Application Designer publish migration is blocked: duplicate active runtime artifacts exist. Samples: {DescribeRows(duplicateArtifacts, "TenantId", "AppCode", "DocumentId", "ArtifactHash", "DuplicateCount")}");
        }

        var duplicatePublishRecords = await schema.ExecuteDataTableAsync(
            "SELECT TenantId, AppCode, DocumentId, ArtifactHash, COUNT(1) AS DuplicateCount FROM app_designer_publish_records WHERE IsDeleted = 0 AND OperationType = 'Publish' GROUP BY TenantId, AppCode, DocumentId, ArtifactHash HAVING COUNT(1) > 1 LIMIT 10;",
            cancellationToken);
        if (duplicatePublishRecords.Rows.Count > 0)
        {
            throw new InvalidOperationException($"Application Designer publish migration is blocked: duplicate active Publish records exist. Samples: {DescribeRows(duplicatePublishRecords, "TenantId", "AppCode", "DocumentId", "ArtifactHash", "DuplicateCount")}");
        }

        var duplicateOperationIds = await schema.ExecuteDataTableAsync(
            "SELECT TenantId, AppCode, OperationId, COUNT(1) AS DuplicateCount FROM app_designer_publish_records WHERE IsDeleted = 0 AND OperationId IS NOT NULL AND trim(OperationId) <> '' GROUP BY TenantId, AppCode, OperationId HAVING COUNT(1) > 1 LIMIT 10;",
            cancellationToken);
        if (duplicateOperationIds.Rows.Count > 0)
        {
            throw new InvalidOperationException($"Application Designer publish migration is blocked: duplicate active OperationIds exist. Samples: {DescribeRows(duplicateOperationIds, "TenantId", "AppCode", "OperationId", "DuplicateCount")}");
        }
    }

    private static string DescribeRows(DataTable table, params string[] columns) =>
        string.Join("; ", table.Rows.Cast<DataRow>().Select(row => string.Join(", ", columns.Select(column => $"{column}={row[column]}"))));

    private static async Task DropLegacyColumnIfExistsAsync(ISqlSugarClient db, string columnName, CancellationToken cancellationToken)
    {
        var result = await new SqliteSchemaExecutor(db).ExecuteDataTableAsync($"SELECT COUNT(1) AS ColumnCount FROM pragma_table_info('app_dev_pages') WHERE name = '{columnName}'", cancellationToken);
        var count = result.Rows.Count > 0 && int.TryParse(result.Rows[0]["ColumnCount"]?.ToString(), out var parsed) ? parsed : 0;
        if (count > 0)
        {
            await new SqliteSchemaExecutor(db).ExecuteNonQueryAsync($"ALTER TABLE \"app_dev_pages\" DROP COLUMN \"{columnName}\";", cancellationToken);
        }
    }
}
