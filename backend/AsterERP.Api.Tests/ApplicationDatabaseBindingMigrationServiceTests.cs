using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Tests.Support;
using AsterERP.Contracts.ApplicationConsole;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDatabaseBindingMigrationServiceTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"astererp-binding-migration-{Guid.NewGuid():N}.db");
    private readonly string contentRootPath = Path.Combine(Path.GetTempPath(), $"astererp-binding-migration-keys-{Guid.NewGuid():N}");

    public ApplicationDatabaseBindingMigrationServiceTests()
    {
        Directory.CreateDirectory(contentRootPath);
    }

    [Fact]
    public async Task MigrateAsync_converts_legacy_binding_and_reports_invalid_rows_without_dropping_them()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemTenantAppEntity>();
        var protector = new ApplicationConnectionStringProtector(DataProtectionProvider.Create(contentRootPath));
        var resolver = new ApplicationDatabaseBindingResolver(
            protector,
            new ApplicationManagedSqliteDatabaseResolver(new TestHostEnvironment(contentRootPath)));
        await db.Insertable(new[]
        {
            new SystemTenantAppEntity
            {
                Id = "legacy-app",
                TenantId = "tenant-a",
                AppCode = "MES",
                ConfigJson = "{\"database\":{\"provider\":\"sqlite3\",\"connectionString\":\"Data Source=mes.db\",\"databaseName\":\"mes.db\"}}",
                CreatedTime = DateTime.UtcNow,
                IsDeleted = false
            },
            new SystemTenantAppEntity
            {
                Id = "invalid-app",
                TenantId = "tenant-a",
                AppCode = "WMS",
                ConfigJson = "{\"applicationDatabase\":{\"provider\":\"Sqlite\",\"connectionStringCipherText\":\"broken\"}}",
                CreatedTime = DateTime.UtcNow,
                IsDeleted = false
            }
        }).ExecuteCommandAsync();

        var service = new ApplicationDatabaseBindingMigrationService(
            db,
            resolver,
            NullLogger<ApplicationDatabaseBindingMigrationService>.Instance);

        var report = await service.MigrateAsync();
        var migrated = await db.Queryable<SystemTenantAppEntity>().FirstAsync(item => item.Id == "legacy-app");

        Assert.Equal(2, report.Scanned);
        Assert.Equal(1, report.Migrated);
        Assert.Equal(1, report.Failed);
        Assert.Contains("applicationDatabase", migrated.ConfigJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"connectionString\"", migrated.ConfigJson, StringComparison.Ordinal);
        Assert.Equal(ApplicationDatabaseBindingStatus.Ready, resolver.ResolveStatus(migrated.ConfigJson).Status);
        Assert.Contains(report.Items, item => item.AppCode == "WMS" && item.Outcome == "InvalidConfiguration");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }

            if (Directory.Exists(contentRootPath))
            {
                Directory.Delete(contentRootPath, true);
            }
        }
        catch (IOException)
        {
        }
    }

    private SqlSugarClient CreateDb() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source={databasePath}",
        DbType = DbType.Sqlite,
        InitKeyType = InitKeyType.Attribute,
        IsAutoCloseConnection = true
    });
}
