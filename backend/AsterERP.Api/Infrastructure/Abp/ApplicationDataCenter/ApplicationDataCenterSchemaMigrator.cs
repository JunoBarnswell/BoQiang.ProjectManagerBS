using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;

public sealed class ApplicationDataCenterSchemaMigrator(ApplicationMappingCacheMigrationService? mappingCacheMigrationService = null)
{
    private readonly ApplicationMappingCacheMigrationService mappingCacheMigrationService = mappingCacheMigrationService ?? new();
    private static readonly Type[] EntityTypes =
    [
        typeof(ApplicationDataSourceEntity), typeof(ApplicationQueryDatasetEntity),
        typeof(ApplicationApiServiceEntity), typeof(ApplicationConnectionCheckTaskEntity),
        typeof(ApplicationConnectionCheckRunEntity), typeof(ApplicationDataCenterDictionaryEntity),
        typeof(ApplicationDataEntityDefinitionEntity), typeof(ApplicationDataFieldDefinitionEntity),
        typeof(ApplicationDataImportBatchEntity), typeof(ApplicationDataModelDesignEntity),
        typeof(ApplicationDataObjectReferenceEntity), typeof(ApplicationIntegrationTaskEntity),
        typeof(ApplicationIntegrationTaskRunEntity), typeof(ApplicationMicroflowEntity), typeof(ApplicationMicroflowRevisionEntity),
        typeof(ApplicationSqlScriptAuditEntity), typeof(ApplicationDataMutationLedgerEntity), typeof(ApplicationDataCenterPublishedSnapshot),
        typeof(ApplicationDataSourceCatalogSnapshotEntity), typeof(ApplicationDataSourceSchemaChangePlanEntity),
        typeof(ApplicationDataSourceSqlitePathApprovalEntity), typeof(ApplicationDataSourceSqlitePathApprovalAuditEntity)
        , typeof(ApplicationMappingCacheEntity), typeof(ApplicationMappingCacheColumnEntity), typeof(ApplicationMappingCacheParameterEntity)
    ];

    public Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.CodeFirst.InitTables(EntityTypes);
        var schema = new SqliteSchemaExecutor(db);
        EnsureTypedWorkbenchFields(schema);
        EnsureSchemaChangePlanImpactFields(schema);
        EnsureMutationLedgerFields(schema);
        CreateIndexes(schema);
        return mappingCacheMigrationService.MigrateAsync(db, cancellationToken);
    }

    private static void EnsureSchemaChangePlanImpactFields(SqliteSchemaExecutor schema)
    {
        schema.EnsureColumn(
            "app_data_source_schema_change_plans",
            "EstimatedAffectedRowsStatus",
            "TEXT NULL DEFAULT 'Unknown'");
        schema.EnsureNullableColumn(
            "app_data_source_schema_change_plans",
            "EstimatedAffectedRows",
            "INTEGER NULL");
        schema.EnsureColumn("app_data_source_schema_change_plans", "BeforeColumnsJson", "TEXT NOT NULL DEFAULT '[]'");
        schema.EnsureColumn("app_data_source_schema_change_plans", "AfterColumnsJson", "TEXT NOT NULL DEFAULT '[]'");
        schema.Execute("UPDATE app_data_source_schema_change_plans SET EstimatedAffectedRows = NULL WHERE (EstimatedAffectedRowsStatus IS NULL OR EstimatedAffectedRowsStatus = 'Unknown') AND EstimatedAffectedRows = 0;");
        schema.Execute("UPDATE app_data_source_schema_change_plans SET EstimatedAffectedRowsStatus = CASE WHEN EstimatedAffectedRows IS NULL THEN 'Unknown' ELSE 'Known' END WHERE EstimatedAffectedRowsStatus IS NULL OR EstimatedAffectedRowsStatus = '';");
    }

    private static void EnsureTypedWorkbenchFields(SqliteSchemaExecutor schema)
    {
        schema.EnsureColumn("app_query_datasets", "IsPhysicalView", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("app_query_datasets", "ViewSchemaName", "TEXT NULL");
        schema.EnsureColumn("app_query_datasets", "ViewSql", "TEXT NULL");
        schema.EnsureColumn("app_dictionary_codes", "DataSourceId", "TEXT NULL");
        schema.EnsureColumn("app_data_sources", "SecretRef", "TEXT NULL");
        schema.EnsureColumn("app_data_sources", "LastValidationFingerprint", "TEXT NULL");
        schema.EnsureColumn("app_data_source_catalog_snapshots", "VersionNo", "INTEGER NOT NULL DEFAULT 1");
        schema.EnsureColumn("app_data_source_catalog_snapshots", "PreviousSnapshotId", "TEXT NULL");
        schema.EnsureColumn("app_data_source_catalog_snapshots", "PreviousSnapshotHash", "TEXT NULL");
        schema.EnsureColumn("app_data_source_catalog_snapshots", "ChangeJson", "TEXT NULL");
        schema.EnsureColumn("app_sql_script_audits", "Operation", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_sql_script_audits", "ResourceKind", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_sql_script_audits", "PermissionCode", "TEXT NULL");
        schema.EnsureColumn("app_sql_script_audits", "Outcome", "TEXT NOT NULL DEFAULT 'Pending'");
        schema.EnsureColumn("app_sql_script_audits", "FailureCode", "TEXT NULL");
        schema.EnsureColumn("app_sql_script_audits", "Provider", "TEXT NULL");
        schema.EnsureColumn("app_sql_script_audits", "TimeoutMs", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("app_sql_script_audits", "CancellationRequested", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("app_sql_script_audits", "ActorUserId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_sql_script_audits", "OccurredAt", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_sql_script_audits", "RequestHash", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_sql_script_audits", "RedactedDetailsJson", "TEXT NOT NULL DEFAULT '{}'");
        schema.EnsureColumn("app_mapping_caches", "Provider", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_mapping_caches", "SchemaName", "TEXT NULL");
        schema.EnsureColumn("app_mapping_caches", "SourceResourceId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_mapping_caches", "ObjectName", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_mapping_caches", "LastRefreshedAt", "TEXT NULL");
        schema.EnsureColumn("app_mapping_caches", "LastValidatedAt", "TEXT NULL");
        schema.EnsureNullableColumn("app_mapping_caches", "LastRowCount", "INTEGER NULL");
        schema.EnsureColumn("app_mapping_caches", "LastValidationStatus", "TEXT NULL");
        schema.EnsureColumn("app_mapping_caches", "LastValidationMessage", "TEXT NULL");
        schema.EnsureColumn("app_mapping_cache_parameters", "ColumnName", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_mapping_cache_columns", "TenantId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_mapping_cache_columns", "AppCode", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_mapping_cache_parameters", "TenantId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_mapping_cache_parameters", "AppCode", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_mapping_cache_parameters", "ResourceId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_mapping_cache_parameters", "ColumnResourceId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("app_mapping_cache_columns", "SourceResourceId", "TEXT NOT NULL DEFAULT ''");
        schema.Execute("UPDATE app_query_datasets SET IsPhysicalView = 1, ViewSchemaName = json_extract(ConfigJson, '$.schemaName'), ViewSql = json_extract(ConfigJson, '$.sql') WHERE IsPhysicalView = 0 AND json_extract(ConfigJson, '$.workbenchKind') = 'PhysicalView';");
        schema.Execute("UPDATE app_dictionary_codes SET DataSourceId = json_extract(ConfigJson, '$.dataSourceId') WHERE (DataSourceId IS NULL OR DataSourceId = '') AND json_extract(ConfigJson, '$.workbenchKind') = 'MappingCache';");
        schema.Execute("INSERT OR IGNORE INTO app_mapping_caches (Id, TenantId, AppCode, DataSourceId, CacheKey, CacheName, Provider, ObjectName, Status, VersionNo, CreatedBy, CreatedTime, Remark, IsDeleted) SELECT Id, TenantId, AppCode, COALESCE(DataSourceId, json_extract(ConfigJson, '$.dataSourceId')), ObjectCode, ObjectName, COALESCE(json_extract(ConfigJson, '$.provider'), 'Sqlite'), COALESCE(json_extract(ConfigJson, '$.source.objectName'), ''), Status, VersionNo, CreatedBy, CreatedTime, Remark, IsDeleted FROM app_dictionary_codes WHERE ObjectType = 'MappingCache' AND IsDeleted = 0;");
    }

    private static void EnsureMutationLedgerFields(SqliteSchemaExecutor schema)
    {
        schema.EnsureColumn("app_data_mutation_ledgers", "LeaseExpiresAt", "TEXT NULL");
        schema.EnsureColumn("app_data_mutation_ledgers", "LeaseToken", "TEXT NULL");
        schema.EnsureColumn("app_data_mutation_ledgers", "StatusHistoryJson", "TEXT NULL");
    }

    private static void CreateIndexes(SqliteSchemaExecutor schema)
    {
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_app_data_sources_workspace ON app_data_sources(TenantId, AppCode, Status) WHERE IsDeleted = 0;",
            "CREATE INDEX IF NOT EXISTS idx_app_query_datasets_workspace ON app_query_datasets(TenantId, AppCode, Status) WHERE IsDeleted = 0;",
            "CREATE INDEX IF NOT EXISTS idx_app_microflows_workspace ON app_microflows(TenantId, AppCode, Status) WHERE IsDeleted = 0;",
            "CREATE INDEX IF NOT EXISTS idx_app_data_model_designs_workspace ON app_data_model_designs(TenantId, AppCode, Status) WHERE IsDeleted = 0;",
            "CREATE INDEX IF NOT EXISTS idx_app_api_services_workspace_route ON app_api_services(TenantId, AppCode, RoutePath, HttpMethod) WHERE IsDeleted = 0;",
            "CREATE INDEX IF NOT EXISTS idx_app_integration_tasks_workspace ON app_integration_tasks(TenantId, AppCode, Status) WHERE IsDeleted = 0;",
            "CREATE INDEX IF NOT EXISTS idx_app_dictionary_codes_workspace ON app_dictionary_codes(TenantId, AppCode, Status) WHERE IsDeleted = 0;",
            "CREATE INDEX IF NOT EXISTS idx_app_data_entity_definitions_workspace ON app_data_entity_definitions(TenantId, AppCode, ModelId) WHERE IsDeleted = 0;",
            "CREATE INDEX IF NOT EXISTS idx_app_data_field_definitions_entity ON app_data_field_definitions(TenantId, AppCode, EntityId, SortOrder) WHERE IsDeleted = 0;",
            "CREATE INDEX IF NOT EXISTS idx_app_data_object_references_target ON app_data_object_references(TenantId, AppCode, TargetModule, TargetObjectId) WHERE IsDeleted = 0;",
            "CREATE INDEX IF NOT EXISTS idx_app_connection_check_runs_task ON app_connection_check_runs(TenantId, AppCode, TaskId, StartedAt);",
            "CREATE INDEX IF NOT EXISTS idx_app_integration_task_runs_task ON app_integration_task_runs(TenantId, AppCode, TaskId, StartedAt);",
            "CREATE INDEX IF NOT EXISTS idx_app_sql_script_audits_workspace ON app_sql_script_audits(TenantId, AppCode, CreatedTime);"
            ,"CREATE UNIQUE INDEX IF NOT EXISTS idx_app_data_center_snapshots_version ON app_data_center_published_snapshots(TenantId, AppCode, ModuleKey, ObjectId, VersionNo);"
            ,"CREATE INDEX IF NOT EXISTS idx_app_data_center_snapshots_runtime ON app_data_center_published_snapshots(TenantId, AppCode, ModuleKey, ObjectId, VersionNo);"
            ,"CREATE INDEX IF NOT EXISTS idx_app_sql_script_audits_security_event ON app_sql_script_audits(TenantId, AppCode, OccurredAt, Operation, Outcome);"
             ,"CREATE UNIQUE INDEX IF NOT EXISTS ux_app_data_mutation_ledger_request ON app_data_mutation_ledgers(TenantId, AppCode, ActorUserId, Operation, RequestHash) WHERE IsDeleted = 0;"
             ,"CREATE INDEX IF NOT EXISTS idx_app_data_mutation_ledger_status ON app_data_mutation_ledgers(TenantId, AppCode, Status, ReservedAt);"
             ,"CREATE INDEX IF NOT EXISTS idx_app_data_mutation_ledger_lease ON app_data_mutation_ledgers(TenantId, AppCode, Status, LeaseExpiresAt);"
        };

        indexes = [.. indexes, "CREATE INDEX IF NOT EXISTS idx_app_data_source_catalog_snapshots_latest ON app_data_source_catalog_snapshots(TenantId, AppCode, DataSourceId, CapturedAt);"];
        indexes = [.. indexes, "CREATE INDEX IF NOT EXISTS idx_app_data_source_schema_change_plans_lookup ON app_data_source_schema_change_plans(TenantId, AppCode, DataSourceId, PlanHash, PlannedAt);"];
        indexes = [.. indexes, "CREATE INDEX IF NOT EXISTS idx_app_data_source_sqlite_path_approvals_lookup ON app_data_source_sqlite_path_approvals(TenantId, AppCode, DataSourceId, Path, Status, ExpiresAt);"];
        indexes = [.. indexes, "CREATE INDEX IF NOT EXISTS idx_app_data_source_sqlite_path_approval_audits_lookup ON app_data_source_sqlite_path_approval_audits(TenantId, AppCode, DataSourceId, OccurredAt);"];
        indexes = [.. indexes, "CREATE UNIQUE INDEX IF NOT EXISTS ux_app_microflow_revisions_number ON app_microflow_revisions(TenantId, AppCode, MicroflowId, RevisionNo) WHERE IsDeleted = 0;"];
        indexes = [.. indexes, "CREATE INDEX IF NOT EXISTS idx_app_microflow_revisions_lookup ON app_microflow_revisions(TenantId, AppCode, MicroflowId, RevisionNo DESC) WHERE IsDeleted = 0;"];

        foreach (var index in indexes)
        {
            schema.Execute(index);
        }
    }
}
