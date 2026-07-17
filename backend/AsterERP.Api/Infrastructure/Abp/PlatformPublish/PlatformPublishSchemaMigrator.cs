using AsterERP.Api.Infrastructure.Database;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.PlatformPublish;

public sealed class PlatformPublishSchemaMigrator
{
    public Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schema = new SqliteSchemaExecutor(db);

        CreateTables(schema);
        EnsureCompatibility(schema);
        CreateIndexes(schema);
        return Task.CompletedTask;
    }

    private static void CreateTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_application_publish_profiles (
    Id TEXT NOT NULL PRIMARY KEY, AppCode TEXT NOT NULL, TenantScope TEXT NOT NULL DEFAULT 'All',
    RuntimeIdentifier TEXT NOT NULL DEFAULT 'win-x64', SelfContained INTEGER NOT NULL DEFAULT 1,
    OutputRoot TEXT NULL, FrontendBasePath TEXT NULL, BackendHost TEXT NULL, BackendPort INTEGER NULL,
    FrontendApiBaseUrl TEXT NULL, KeepSuccessfulCount INTEGER NOT NULL DEFAULT 5,
    IncludeFrontend INTEGER NOT NULL DEFAULT 1, IncludeBackend INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_application_publish_tasks (
    Id TEXT NOT NULL PRIMARY KEY, AppId TEXT NOT NULL, AppCode TEXT NOT NULL, TenantId TEXT NULL,
    Version TEXT NULL, Status TEXT NOT NULL, Stage TEXT NOT NULL, ProgressPercent INTEGER NOT NULL DEFAULT 0,
    StartedAt TEXT NULL, FinishedAt TEXT NULL, DurationMs INTEGER NOT NULL DEFAULT 0,
    SourceProjectPath TEXT NULL, ReleasePath TEXT NULL, ArtifactPath TEXT NULL, ErrorMessage TEXT NULL,
    TraceId TEXT NOT NULL, RuntimeIdentifier TEXT NOT NULL DEFAULT 'win-x64', SelfContained INTEGER NOT NULL DEFAULT 1,
    IncludeFrontend INTEGER NOT NULL DEFAULT 1, IncludeBackend INTEGER NOT NULL DEFAULT 1,
    CleanOutput INTEGER NOT NULL DEFAULT 0, BackendHost TEXT NOT NULL DEFAULT '127.0.0.1',
    BackendPort INTEGER NOT NULL DEFAULT 5000, FrontendBasePath TEXT NOT NULL DEFAULT '',
    FrontendApiBaseUrl TEXT NOT NULL DEFAULT '/api', CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_application_publish_logs (
    Id TEXT NOT NULL PRIMARY KEY, TaskId TEXT NOT NULL, Level TEXT NOT NULL, Stage TEXT NOT NULL,
    Message TEXT NOT NULL, TraceId TEXT NOT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_application_publish_artifacts (
    Id TEXT NOT NULL PRIMARY KEY, TaskId TEXT NOT NULL, FileName TEXT NOT NULL, ContentType TEXT NOT NULL,
    SizeBytes INTEGER NOT NULL DEFAULT 0, Sha256 TEXT NOT NULL, StoredPath TEXT NOT NULL, ExpiresAt TEXT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
    }

    private static void EnsureCompatibility(SqliteSchemaExecutor schema)
    {
        schema.EnsureColumn("system_application_publish_profiles", "BackendHost", "TEXT NULL");
        schema.EnsureColumn("system_application_publish_profiles", "BackendPort", "INTEGER NULL");
        schema.EnsureColumn("system_application_publish_profiles", "FrontendApiBaseUrl", "TEXT NULL");
        schema.EnsureColumn("system_application_publish_tasks", "BackendHost", "TEXT NOT NULL DEFAULT '127.0.0.1'");
        schema.EnsureColumn("system_application_publish_tasks", "BackendPort", "INTEGER NOT NULL DEFAULT 5000");
        schema.EnsureColumn("system_application_publish_tasks", "FrontendBasePath", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("system_application_publish_tasks", "FrontendApiBaseUrl", "TEXT NOT NULL DEFAULT '/api'");
    }

    private static void CreateIndexes(SqliteSchemaExecutor schema)
    {
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_application_publish_profiles_app ON system_application_publish_profiles(AppCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_application_publish_tasks_app_status ON system_application_publish_tasks(AppCode, Status, CreatedTime) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_application_publish_tasks_appid_time ON system_application_publish_tasks(AppId, CreatedTime) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_application_publish_logs_task_time ON system_application_publish_logs(TaskId, CreatedTime) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_application_publish_artifacts_task_time ON system_application_publish_artifacts(TaskId, CreatedTime) WHERE IsDeleted = 0;");
    }
}
