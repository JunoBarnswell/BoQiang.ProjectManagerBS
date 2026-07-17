using AsterERP.Api.Infrastructure.Database;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.PlatformFoundation;

public sealed class PlatformFoundationSchemaMigrator
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
CREATE TABLE IF NOT EXISTS system_tenants (
    Id TEXT NOT NULL PRIMARY KEY, TenantCode TEXT NOT NULL, TenantName TEXT NOT NULL,
    ShortName TEXT NULL, Status TEXT NOT NULL DEFAULT 'Enabled', ExpiredAt TEXT NULL,
    ContactName TEXT NULL, ContactPhone TEXT NULL, ConfigJson TEXT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_applications (
    Id TEXT NOT NULL PRIMARY KEY, AppCode TEXT NOT NULL, AppName TEXT NOT NULL,
    AppType TEXT NOT NULL DEFAULT 'Business', Icon TEXT NULL, DefaultRoutePath TEXT NULL,
    AdminDefaultRoutePath TEXT NULL, RuntimeDefaultRoutePath TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled', Version TEXT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_tenant_apps (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled', SystemName TEXT NULL, LogoFileId TEXT NULL,
    FaviconFileId TEXT NULL, PrimaryColor TEXT NULL, ExpiredAt TEXT NULL, ConfigJson TEXT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_user_tenant_memberships (
    Id TEXT NOT NULL PRIMARY KEY, UserId TEXT NOT NULL, TenantId TEXT NOT NULL,
    DeptId TEXT NULL, PositionId TEXT NULL, IsTenantAdmin INTEGER NOT NULL DEFAULT 0,
    IsDefault INTEGER NOT NULL DEFAULT 0, Status TEXT NOT NULL DEFAULT 'Enabled',
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_user_app_roles (
    Id TEXT NOT NULL PRIMARY KEY, UserId TEXT NOT NULL, TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL, RoleId TEXT NOT NULL, IsDefault INTEGER NOT NULL DEFAULT 0,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
    }

    private static void EnsureCompatibility(SqliteSchemaExecutor schema)
    {
        schema.EnsureColumn("system_applications", "AdminDefaultRoutePath", "TEXT NULL");
        schema.EnsureColumn("system_applications", "RuntimeDefaultRoutePath", "TEXT NULL");
        schema.Execute("UPDATE system_applications SET AdminDefaultRoutePath = DefaultRoutePath WHERE (AdminDefaultRoutePath IS NULL OR AdminDefaultRoutePath = '') AND DefaultRoutePath IS NOT NULL AND DefaultRoutePath <> '';");
    }

    private static void CreateIndexes(SqliteSchemaExecutor schema)
    {
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_tenants_code ON system_tenants(TenantCode);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_applications_code ON system_applications(AppCode);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_tenant_apps_unique ON system_tenant_apps(TenantId, AppCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_user_tenant_unique ON system_user_tenant_memberships(UserId, TenantId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_user_app_roles_unique ON system_user_app_roles(UserId, TenantId, AppCode, RoleId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_user_app_roles_workspace ON system_user_app_roles(UserId, TenantId, AppCode);");
    }
}
