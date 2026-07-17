using AsterERP.Api.Infrastructure.Abp.PlatformFoundation;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class PlatformFoundationSchemaMigratorTests
{
    [Fact]
    public async Task MigrateAsync_creates_platform_schema_without_core_shell_lifecycle()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:platform-foundation-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        await new PlatformFoundationSchemaMigrator().MigrateAsync(db, CancellationToken.None);

        var tables = db.Ado.GetDataTable("SELECT name FROM sqlite_master WHERE type = 'table'")
            .Rows.Cast<System.Data.DataRow>()
            .Select(row => row["name"]?.ToString())
            .Where(name => name is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("system_tenants", tables);
        Assert.Contains("system_applications", tables);
        Assert.Contains("system_tenant_apps", tables);
        Assert.Contains("system_user_tenant_memberships", tables);
        Assert.Contains("system_user_app_roles", tables);
    }
}
