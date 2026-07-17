using AsterERP.Api.Application.ApplicationConsole;
using Microsoft.Data.Sqlite;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataCenterApplicationDatabaseSchemaInitializerTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"astererp-data-center-schema-{Guid.NewGuid():N}.db");

    [Fact]
    public void Initializes_data_studio_tables_in_application_database()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

        new ApplicationDataCenterApplicationDatabaseSchemaInitializer().Initialize(db);

        Assert.True(db.DbMaintenance.IsAnyTable("app_data_source_sqlite_path_approvals", false));
        Assert.True(db.DbMaintenance.IsAnyTable("app_data_source_sqlite_path_approval_audits", false));
        Assert.True(db.DbMaintenance.IsAnyTable("app_data_source_catalog_snapshots", false));
        Assert.True(db.DbMaintenance.IsAnyTable("app_data_source_schema_change_plans", false));
        Assert.True(db.DbMaintenance.IsAnyTable("app_data_mutation_ledgers", false));
        Assert.True(db.DbMaintenance.IsAnyTable("app_mapping_caches", false));
        db.Dispose();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5 && File.Exists(databasePath); attempt++)
        {
            try
            {
                File.Delete(databasePath);
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(50);
            }
        }
    }
}
