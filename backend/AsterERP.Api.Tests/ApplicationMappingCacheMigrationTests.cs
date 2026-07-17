using AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationMappingCacheMigrationTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"astererp-mapping-cache-migration-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task MigratesLegacySourceAndChildrenToResourceIdsIdempotently()
    {
        using var db = ApplicationDataStudioSqliteFixture.CreateDb(databasePath);
        db.CodeFirst.InitTables<ApplicationDataSourceCatalogSnapshotEntity, ApplicationDataCenterDictionaryEntity>();
        var tableResourceId = ApplicationDataResourceId.Table("ds-legacy", null, "orders");
        var idResourceId = ApplicationDataResourceId.Column(tableResourceId, "id");
        var snapshot = new ApplicationDataSourceCatalogSnapshotEntity
        {
            Id = "snapshot-1",
            TenantId = "tenant-a",
            AppCode = "MES",
            DataSourceId = "ds-legacy",
            Provider = "Sqlite",
            VersionNo = 1,
            SnapshotHash = "hash",
            CatalogJson = ApplicationDataCenterJson.Serialize(new[]
            {
                new ApplicationDataSourceCatalogTableResponse("orders", null, "TABLE", [new("id", "INTEGER", false, true, 1) { ResourceId = idResourceId }], [], [], []) { ResourceId = tableResourceId }
            })
        };
        await db.Insertable(snapshot).ExecuteCommandAsync();
        await db.Insertable(new ApplicationDataCenterDictionaryEntity
        {
            Id = "legacy-cache",
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.DictionaryCode,
            ObjectCode = "orders_cache",
            ObjectName = "Orders cache",
            ObjectType = "MappingCache",
            DataSourceId = "ds-legacy",
            ConfigJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?>
            {
                ["dataSourceId"] = "ds-legacy",
                ["provider"] = "Sqlite",
                ["source"] = new Dictionary<string, object?>
                {
                    ["objectName"] = "orders",
                    ["columns"] = new[] { new Dictionary<string, object?> { ["sourceName"] = "id", ["targetName"] = "order_id" } }
                }
            })
        }).ExecuteCommandAsync();

        var migrator = new ApplicationDataCenterSchemaMigrator();
        await migrator.MigrateAsync(db, CancellationToken.None);
        await migrator.MigrateAsync(db, CancellationToken.None);

        var cache = await db.Queryable<ApplicationMappingCacheEntity>().SingleAsync(item => item.Id == "legacy-cache");
        var columns = await db.Queryable<ApplicationMappingCacheColumnEntity>().Where(item => item.CacheId == cache.Id && !item.IsDeleted).ToListAsync();
        Assert.Equal(tableResourceId, cache.SourceResourceId);
        Assert.Equal(ApplicationDataCenterObjectStatus.Normal, cache.Status);
        Assert.Single(columns);
        Assert.Equal(idResourceId, columns[0].SourceResourceId);
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM app_mapping_cache_columns WHERE CacheId = 'legacy-cache' AND IsDeleted = 0"));
    }

    public void Dispose()
    {
        try { if (File.Exists(databasePath)) File.Delete(databasePath); } catch (IOException) { }
    }
}
