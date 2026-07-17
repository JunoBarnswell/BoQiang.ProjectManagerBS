using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataSourceWorkbenchService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDataSourceService dataSourceService)
{
    public async Task<ApplicationDataSourceWorkbenchResponse> GetAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        var dataSource = await dataSourceService.GetAsync(dataSourceId, cancellationToken);
        var stats = await GetStatsAsync(dataSourceId, cancellationToken);
        return new ApplicationDataSourceWorkbenchResponse(
            dataSource,
            ApplicationDataSourceConnectionFactory.IsDatabaseType(dataSource.ObjectType),
            dataSource.Endpoint,
            dataSource.LastValidationStatus,
            dataSource.LastValidationMessage,
            dataSource.LastValidatedAt,
            stats);
    }

    public async Task<ApplicationDataSourceRuntimeCheckResponse> GetRuntimeChecksAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await EnsureDataSourceAsync(db, workspace, dataSourceId, cancellationToken);

        var runs = await db.Queryable<ApplicationConnectionCheckRunEntity>()
            .Where(item =>
                item.DataSourceId == dataSourceId &&
                !item.IsDeleted)
            .OrderBy(item => item.StartedAt, OrderByType.Desc)
            .Take(20)
            .ToListAsync(cancellationToken);

        return new ApplicationDataSourceRuntimeCheckResponse(
            runs.Select(MapRun).ToArray(),
            await GetStatsAsync(dataSourceId, cancellationToken));
    }

    public async Task<ApplicationDataSourceWorkbenchStatsResponse> GetStatsAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(db, workspace, dataSourceId, cancellationToken);
        var tableCount = 0;
        if (ApplicationDataSourceConnectionFactory.IsDatabaseType(dataSource.ObjectType))
        {
            try
            {
                tableCount = (await dataSourceService.GetTablesAsync(dataSourceId, cancellationToken))
                    .Count(item => !string.Equals(item.TableType, "VIEW", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                tableCount = 0;
            }
        }

        var viewCount = await db.Queryable<ApplicationQueryDatasetEntity>()
            .Where(item =>
                item.SourceObjectId == dataSourceId &&
                item.IsPhysicalView &&
                !item.IsDeleted)
            .CountAsync(cancellationToken);
        var microflowCount = await db.Queryable<ApplicationDataObjectReferenceEntity>()
            .Where(item =>
                item.TargetObjectId == dataSourceId &&
                item.SourceModule == ApplicationDataCenterModuleKey.Microflow &&
                !item.IsDeleted)
            .CountAsync(cancellationToken);
        var cacheCount = await db.Queryable<ApplicationMappingCacheEntity>()
            .Where(item =>
                item.DataSourceId == dataSourceId &&
                !item.IsDeleted)
            .CountAsync(cancellationToken);
        var runCount = await db.Queryable<ApplicationConnectionCheckRunEntity>()
            .Where(item =>
                item.DataSourceId == dataSourceId &&
                !item.IsDeleted)
            .CountAsync(cancellationToken);
        var syncCount = await db.Queryable<ApplicationIntegrationTaskEntity>()
            .Where(item =>
                (item.SourceObjectId == dataSourceId || item.TargetObjectId == dataSourceId) &&
                !item.IsDeleted)
            .CountAsync(cancellationToken);

        return new ApplicationDataSourceWorkbenchStatsResponse(tableCount, viewCount, microflowCount, cacheCount, runCount, syncCount);
    }

    private static ApplicationConnectionCheckRunResponse MapRun(ApplicationConnectionCheckRunEntity entity) =>
        new(
            entity.Id,
            entity.TemplateCode,
            entity.Result,
            entity.StartedAt,
            entity.FinishedAt,
            entity.DurationMs,
            entity.ErrorMessage,
            entity.ResultJson);

    private static async Task<ApplicationDataSourceEntity> EnsureDataSourceAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string dataSourceId,
        CancellationToken cancellationToken)
    {
        return await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item =>
                item.Id == dataSourceId &&
                !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

}
