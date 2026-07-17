using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDesignerMigrationRunTests
{
    [Fact]
    public async Task Expired_migration_lock_is_closed_before_a_new_run_is_acquired()
    {
        using var db = CreateDatabase();
        await MigrateAsync(db);
        var service = new ApplicationDesignerMigrationRunService();
        var first = await service.AcquireAsync(db, "tenant-a", "MES", "latest", "{\"pages\":[]}", null, null, "user-a", null);

        await Assert.ThrowsAsync<ValidationException>(() => service.AcquireAsync(db, "tenant-a", "MES", "latest", "{\"pages\":[]}", null, null, "user-a", null));
        await db.Updateable<ApplicationDesignerMigrationRunEntity>()
            .SetColumns(item => new ApplicationDesignerMigrationRunEntity { LockExpiresTime = DateTime.UtcNow.AddMinutes(-1) })
            .Where(item => item.Id == first.Id)
            .ExecuteCommandAsync(CancellationToken.None);

        var second = await service.AcquireAsync(db, "tenant-a", "MES", "latest", "{\"pages\":[]}", null, null, "user-a", null);
        var persistedFirst = await db.Queryable<ApplicationDesignerMigrationRunEntity>().Where(item => item.Id == first.Id).SingleAsync();
        Assert.Equal("Failed", persistedFirst.Status);
        Assert.Contains("migration.lock_expired", persistedFirst.DiagnosticsJson, StringComparison.Ordinal);
        Assert.Equal("Running", second.Status);
    }

    [Fact]
    public async Task Migration_backup_hash_mismatch_blocks_new_run()
    {
        using var db = CreateDatabase();
        await MigrateAsync(db);
        var service = new ApplicationDesignerMigrationRunService();
        var first = await service.AcquireAsync(db, "tenant-a", "MES", "latest", "{\"pages\":[]}", null, null, "user-a", null);
        await db.Updateable<ApplicationDesignerMigrationRunEntity>()
            .SetColumns(item => new ApplicationDesignerMigrationRunEntity { BackupSha256 = "tampered", LockExpiresTime = DateTime.UtcNow.AddMinutes(-1) })
            .Where(item => item.Id == first.Id)
            .ExecuteCommandAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() => service.AcquireAsync(db, "tenant-a", "MES", "latest", "{\"pages\":[]}", null, null, "user-a", null));
        Assert.Equal("Running", (await db.Queryable<ApplicationDesignerMigrationRunEntity>().Where(item => item.Id == first.Id).SingleAsync()).Status);
    }

    private static async Task MigrateAsync(SqlSugarClient db) =>
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);

    private static SqlSugarClient CreateDatabase() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:designer-migration-run-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });
}
