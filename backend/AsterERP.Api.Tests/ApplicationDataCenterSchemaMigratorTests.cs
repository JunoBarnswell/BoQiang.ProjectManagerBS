using AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDataCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataCenterSchemaMigratorTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"astererp-schema-migrator-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task LegacySchemaChangePlanTableIsRebuiltWithNullableImpactAndUnknownStatus()
    {
        using var db = ApplicationDataStudioSqliteFixture.CreateDb(databasePath);
        db.Ado.ExecuteCommand("""
            CREATE TABLE app_data_source_schema_change_plans (
                Id TEXT PRIMARY KEY,
                CreatedBy TEXT NULL,
                CreatedTime TEXT NOT NULL,
                UpdatedBy TEXT NULL,
                UpdatedTime TEXT NULL,
                DeletedBy TEXT NULL,
                DeletedTime TEXT NULL,
                IsDeleted INTEGER NOT NULL,
                Remark TEXT NULL,
                TenantId TEXT NOT NULL,
                AppCode TEXT NOT NULL,
                DataSourceId TEXT NOT NULL,
                Provider TEXT NOT NULL,
                Operation TEXT NOT NULL,
                Target TEXT NOT NULL,
                SqlPreview TEXT NOT NULL,
                RisksJson TEXT NOT NULL,
                RiskLevel TEXT NOT NULL,
                RequiresLock INTEGER NOT NULL,
                EstimatedAffectedRows INTEGER NOT NULL DEFAULT 0,
                DependenciesJson TEXT NOT NULL,
                RequiresConfirmation INTEGER NOT NULL,
                Reversible INTEGER NOT NULL,
                PlanHash TEXT NOT NULL,
                PlannedAt TEXT NOT NULL,
                Status TEXT NOT NULL
            );
            CREATE INDEX idx_legacy_schema_plan_hash ON app_data_source_schema_change_plans(PlanHash);
            INSERT INTO app_data_source_schema_change_plans
                (Id, CreatedTime, IsDeleted, TenantId, AppCode, DataSourceId, Provider, Operation, Target, SqlPreview, RisksJson, RiskLevel, RequiresLock, EstimatedAffectedRows, DependenciesJson, RequiresConfirmation, Reversible, PlanHash, PlannedAt, Status)
            VALUES
                ('legacy-plan', '2026-07-12T00:00:00Z', 0, 'tenant-a', 'MES', 'ds-legacy', 'Sqlite', 'CreateTable', 'legacy_table', 'CREATE TABLE legacy_table (id INTEGER)', '[]', 'medium', 1, 0, '[]', 1, 1, 'legacy-hash', '2026-07-12T00:00:00Z', 'Planned');
            """);

        await new ApplicationDataCenterSchemaMigrator().MigrateAsync(db, CancellationToken.None);

        var columns = db.Ado.GetDataTable("SELECT name, \"notnull\" FROM pragma_table_info('app_data_source_schema_change_plans') ORDER BY cid");
        var estimate = columns.Rows.Cast<System.Data.DataRow>().Single(row => Convert.ToString(row["name"]) == "EstimatedAffectedRows");
        Assert.Equal(0, Convert.ToInt32(estimate["notnull"]));
        Assert.Contains(columns.Rows.Cast<System.Data.DataRow>(), row => Convert.ToString(row["name"]) == "EstimatedAffectedRowsStatus");

        var migrated = await db.Queryable<ApplicationDataSourceSchemaChangePlanEntity>()
            .SingleAsync(item => item.Id == "legacy-plan");
        Assert.Null(migrated.EstimatedAffectedRows);
        Assert.Equal("Unknown", migrated.EstimatedAffectedRowsStatus);
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'idx_legacy_schema_plan_hash'"));

        var ledgerColumns = db.Ado.GetDataTable("SELECT name FROM pragma_table_info('app_data_mutation_ledgers')");
        Assert.Contains(ledgerColumns.Rows.Cast<System.Data.DataRow>(), row => Convert.ToString(row["name"]) == "LeaseExpiresAt");
        Assert.Contains(ledgerColumns.Rows.Cast<System.Data.DataRow>(), row => Convert.ToString(row["name"]) == "LeaseToken");
        Assert.Contains(ledgerColumns.Rows.Cast<System.Data.DataRow>(), row => Convert.ToString(row["name"]) == "StatusHistoryJson");
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'ux_app_data_mutation_ledger_request'"));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
        catch (IOException)
        {
        }
    }
}
