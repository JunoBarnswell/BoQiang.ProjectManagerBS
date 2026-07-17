using AsterERP.Api.Infrastructure.Database;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.RuntimeCore;

public sealed class RuntimeCoreSchemaMigrator
{
    public Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schema = new SqliteSchemaExecutor(db);

        CreateRuntimeDataModelTables(schema);
        return Task.CompletedTask;
    }

    private static void CreateRuntimeDataModelTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_data_models (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ModelCode TEXT NOT NULL,
    ModelName TEXT NOT NULL,
    ProviderKey TEXT NOT NULL,
    KeyField TEXT NOT NULL DEFAULT 'id',
    PermissionCode TEXT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    Status TEXT NOT NULL DEFAULT 'Published',
    SchemaJson TEXT NOT NULL,
    CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL,
    UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL,
    DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Remark TEXT NULL
);
""");

        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_tenant_grid_views (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    PageCode TEXT NOT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    ViewJson TEXT NOT NULL,
    CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL,
    UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL,
    DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Remark TEXT NULL
);
""");

        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_user_grid_views (
    Id TEXT NOT NULL PRIMARY KEY,
    UserId TEXT NOT NULL,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    PageCode TEXT NOT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    ViewJson TEXT NOT NULL,
    CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL,
    UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL,
    DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Remark TEXT NULL
);
""");

        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_data_models_published_unique ON system_data_models(TenantId, AppCode, ModelCode, Status) WHERE IsDeleted = 0 AND Status = 'Published';");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_data_models_runtime_lookup ON system_data_models(TenantId, AppCode, ModelCode, Status, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_tenant_grid_views_unique ON system_tenant_grid_views(TenantId, AppCode, PageCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_tenant_grid_views_lookup ON system_tenant_grid_views(TenantId, AppCode, PageCode, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_user_grid_views_unique ON system_user_grid_views(UserId, TenantId, AppCode, PageCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_user_grid_views_lookup ON system_user_grid_views(UserId, TenantId, AppCode, PageCode, IsDeleted);");
    }
}
