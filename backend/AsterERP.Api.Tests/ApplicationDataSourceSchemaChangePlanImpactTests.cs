using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceSchemaChangePlanImpactTests : IDisposable
{
    private readonly ApplicationDataStudioSqliteFixture fixture = new();

    [Fact]
    public async Task UnknownAffectedRowsRemainNullAndPersistExplicitUnknownStatus()
    {
        var request = new ApplicationDataSourceCreateTableRequest(
            "impact_unknown",
            null,
            "Impact unknown",
            null,
            [new("id", "INTEGER", false, true, null, null)]);

        var plan = await fixture.CreateTableWorkbenchService().CreateTablePlanAsync(fixture.DataSourceId, request);
        var persisted = await fixture.AppDb.Queryable<ApplicationDataSourceSchemaChangePlanEntity>()
            .SingleAsync(item => item.PlanHash == plan.PlanHash);

        Assert.Null(plan.EstimatedAffectedRows);
        Assert.Null(persisted.EstimatedAffectedRows);
        Assert.Equal("Unknown", persisted.EstimatedAffectedRowsStatus);
    }

    [Fact]
    public async Task ProviderWithoutSchemaSupportRejectsSchemaQualifiedTablePlansAndPreviews()
    {
        var request = new ApplicationDataSourceCreateTableRequest(
            "schema_rejected",
            "attached",
            "Schema rejected",
            null,
            [new("id", "INTEGER", false, true, null, null)]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.CreateTableWorkbenchService().CreateTablePlanAsync(fixture.DataSourceId, request));

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.CreateTableWorkbenchService().PreviewTableAsync(fixture.DataSourceId, "attached.schema_rejected", 10));
    }

    [Fact]
    public async Task DeployWithMissingPersistedPlanRegeneratesAndFailsClosedOnHashMismatch()
    {
        var request = new ApplicationDataSourceCreateTableRequest(
            "missing_plan_recovery",
            null,
            "Missing plan recovery",
            null,
            [new("id", "INTEGER", false, true, null, null)]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.CreateTableWorkbenchService().DeployTablePlanAsync(
                fixture.DataSourceId,
                new ApplicationDataSourceSchemaChangePlanRequest("stale-plan-hash", request, true)));

        Assert.Contains("SchemaChangePlan 已变化", exception.Message);
        Assert.True(await fixture.AppDb.Queryable<ApplicationDataSourceSchemaChangePlanEntity>()
            .AnyAsync(item => item.Target == "missing_plan_recovery" && item.Status == "Planned"));
    }

    [Fact]
    public async Task AlterTablePlanReadsCurrentSchemaAndPersistsStrongPlan()
    {
        var current = await fixture.CreateTableWorkbenchService().GetTableAsync(fixture.DataSourceId, "dc_people");
        var plan = await fixture.CreateTableWorkbenchService().CreateAlterTablePlanAsync(
            fixture.DataSourceId,
            new ApplicationDataSourceAlterTableRequest(
                "dc_people",
                null,
                [.. current.Columns.Select(item => new ApplicationDataSourceCreateTableColumnRequest(item.ColumnName, item.DataType, item.Nullable, item.PrimaryKey, null, null)),
                    new("email", "TEXT", true, false, null, null)]));

        Assert.Equal("AlterTable", plan.Operation);
        Assert.Contains("ADD COLUMN", plan.SqlPreview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("primary-key:id", plan.Dependencies);
        Assert.Equal("medium", plan.RiskLevel);
        Assert.True(await fixture.AppDb.Queryable<ApplicationDataSourceSchemaChangePlanEntity>()
            .AnyAsync(item => item.PlanHash == plan.PlanHash && item.Operation == "AlterTable" && item.Status == "Planned"));
    }

    [Fact]
    public async Task AlterTablePlanIncludesCatalogDependenciesAndRejectsChangedSnapshot()
    {
        var catalogTable = new ApplicationDataSourceCatalogTableResponse(
            "dc_people",
            null,
            "TABLE",
            [],
            [new("fk_people_department", "FOREIGN KEY", "")],
            [new("ix_dc_people_name", "INDEX", "")],
            [new("tr_dc_people_insert", "TRIGGER", "")]);
        await fixture.AppDb.Insertable(new ApplicationDataSourceCatalogSnapshotEntity
        {
            Id = "catalog-v1",
            TenantId = "tenant-a",
            AppCode = "MES",
            DataSourceId = fixture.DataSourceId,
            Provider = ApplicationDataSourceType.Sqlite,
            SnapshotHash = "catalog-v1",
            VersionNo = 1,
            CapturedAt = DateTime.UtcNow,
            CatalogJson = ApplicationDataCenterJson.Serialize(new[] { catalogTable })
        }).ExecuteCommandAsync();

        var current = await fixture.CreateTableWorkbenchService().GetTableAsync(fixture.DataSourceId, "dc_people");
        var request = new ApplicationDataSourceAlterTableRequest(
            "dc_people",
            null,
            [.. current.Columns.Select(item => new ApplicationDataSourceCreateTableColumnRequest(item.ColumnName, item.DataType, item.Nullable, item.PrimaryKey, null, null)),
                new("email", "TEXT", true, false, null, null)]);
        var plan = await fixture.CreateTableWorkbenchService().CreateAlterTablePlanAsync(fixture.DataSourceId, request);

        Assert.Contains("constraint:fk_people_department", plan.Dependencies);
        Assert.Contains("index:ix_dc_people_name", plan.Dependencies);
        Assert.Contains("trigger:tr_dc_people_insert", plan.Dependencies);

        await fixture.AppDb.Insertable(new ApplicationDataSourceCatalogSnapshotEntity
        {
            Id = "catalog-v2",
            TenantId = "tenant-a",
            AppCode = "MES",
            DataSourceId = fixture.DataSourceId,
            Provider = ApplicationDataSourceType.Sqlite,
            SnapshotHash = "catalog-v2",
            VersionNo = 2,
            CapturedAt = DateTime.UtcNow,
            CatalogJson = ApplicationDataCenterJson.Serialize(new[] { catalogTable with
            {
                Indexes = [new("ix_dc_people_name", "INDEX", ""), new("ix_dc_people_email", "INDEX", "")]
            } })
        }).ExecuteCommandAsync();

        await Assert.ThrowsAsync<ValidationException>(() => fixture.CreateTableWorkbenchService().DeployAlterTablePlanAsync(
            fixture.DataSourceId,
            new ApplicationDataSourceAlterTablePlanRequest(plan.PlanHash, request, true)));
    }

    public void Dispose() => fixture.Dispose();
}
