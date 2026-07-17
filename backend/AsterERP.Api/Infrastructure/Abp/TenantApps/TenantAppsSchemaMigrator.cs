using AsterERP.Api.Infrastructure.Database;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.TenantApps;

public sealed class TenantAppsSchemaMigrator
{
    public Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schema = new SqliteSchemaExecutor(db);

        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_tenant_apps (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled', SystemName TEXT NULL, LogoFileId TEXT NULL,
    FaviconFileId TEXT NULL, PrimaryColor TEXT NULL, ExpiredAt TEXT NULL, ConfigJson TEXT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_tenant_apps_unique ON system_tenant_apps(TenantId, AppCode) WHERE IsDeleted = 0;");
        return Task.CompletedTask;
    }
}
