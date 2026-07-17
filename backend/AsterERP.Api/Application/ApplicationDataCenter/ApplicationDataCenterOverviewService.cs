using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataCenterOverviewService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDataCenterTemplateCatalog templateCatalog)
{
    public IReadOnlyList<ApplicationDataCenterTypeOptionResponse> GetTypeOptions(string? moduleKey) =>
        templateCatalog.GetTypeOptions(moduleKey);

    public IReadOnlyList<ApplicationDataCenterTemplateResponse> GetTemplates(string? moduleKey) =>
        templateCatalog.GetTemplates(moduleKey);

    public async Task<ApplicationDataCenterWorkspaceResponse> GetWorkspaceAsync(
        string? dataSourceId,
        string? moduleKey,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var modules = await GetModulesAsync(cancellationToken);
        var normalizedModuleKey = string.IsNullOrWhiteSpace(moduleKey) ? null : moduleKey.Trim();
        var dataSources = await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(200)
            .ToListAsync(cancellationToken);
        var normalizedDataSourceId = string.IsNullOrWhiteSpace(dataSourceId) ? null : dataSourceId.Trim();
        ApplicationDataCenterObjectDetailResponse? selectedDataSource = null;
        if (!string.IsNullOrWhiteSpace(normalizedDataSourceId))
        {
            var entity = (await db.Queryable<ApplicationDataSourceEntity>()
                .Where(item =>
                    item.Id == normalizedDataSourceId &&
                    !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();
            if (entity is not null)
            {
                selectedDataSource = MapDetail(entity);
            }
        }

        return new ApplicationDataCenterWorkspaceResponse
        {
            ModuleKey = normalizedModuleKey,
            SelectedDataSourceId = normalizedDataSourceId,
            SelectedDataSource = selectedDataSource,
            Modules = modules.ToList(),
            TypeOptions = GetTypeOptions(normalizedModuleKey).ToList(),
            Templates = GetTemplates(normalizedModuleKey).ToList(),
            DataSources = dataSources.Select(MapListItem).ToList(),
            RecentItems = await LoadRecentItemsAsync(db, workspace, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<ApplicationDataCenterModuleOverviewResponse>> GetModulesAsync(
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        return
        [
            await BuildAsync<ApplicationDataSourceEntity>(db, workspace, ApplicationDataCenterModuleKey.DataSource, "数据源管理", "配置和维护应用数据源连接。", PermissionCodes.AppDataCenterDataSourceView, cancellationToken),
            await BuildAsync<ApplicationConnectionCheckTaskEntity>(db, workspace, ApplicationDataCenterModuleKey.ConnectionTest, "数据库连接测试", "测试数据源连通性、认证信息和性能。", PermissionCodes.AppDataCenterConnectionTestView, cancellationToken),
            await BuildAsync<ApplicationMicroflowEntity>(db, workspace, ApplicationDataCenterModuleKey.Microflow, "微流管理", "编排领域对象、变量、接口、CRUD、if/for 和函数链。", PermissionCodes.AppDataCenterMicroflowView, cancellationToken),
            await BuildAsync<ApplicationDataModelDesignEntity>(db, workspace, ApplicationDataCenterModuleKey.DataModel, "数据模型", "管理应用运行时数据模型、字段、来源和发布版本。", PermissionCodes.AppDataCenterDataModelView, cancellationToken),
            await BuildAsync<ApplicationApiServiceEntity>(db, workspace, ApplicationDataCenterModuleKey.ApiService, "API 服务", "管理应用 API 路由、来源绑定、权限和发布状态。", PermissionCodes.AppDataCenterApiServiceView, cancellationToken),
            await BuildAsync<ApplicationDataEntityDefinitionEntity>(db, workspace, ApplicationDataCenterModuleKey.EntityField, "数据实体与字段", "维护实体、字段、关联关系和字段权限。", PermissionCodes.AppDataCenterEntityFieldView, cancellationToken),
            await BuildAsync<ApplicationDataCenterDictionaryEntity>(db, workspace, ApplicationDataCenterModuleKey.DictionaryCode, "字典与编码", "管理字典分类、编码规则和导入导出。", PermissionCodes.AppDataCenterDictionaryCodeView, cancellationToken),
            await BuildAsync<ApplicationQueryDatasetEntity>(db, workspace, ApplicationDataCenterModuleKey.QueryDataset, "查询视图与报表数据集", "创建查询视图和报表数据集。", PermissionCodes.AppDataCenterQueryDatasetView, cancellationToken),
            await BuildAsync<ApplicationIntegrationTaskEntity>(db, workspace, ApplicationDataCenterModuleKey.IntegrationTask, "数据同步与集成任务", "配置同步任务、运行监控和同步日志。", PermissionCodes.AppDataCenterIntegrationTaskView, cancellationToken)
        ];
    }

    private static async Task<ApplicationDataCenterModuleOverviewResponse> BuildAsync<TEntity>(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string moduleKey,
        string title,
        string description,
        string viewPermissionCode,
        CancellationToken cancellationToken)
        where TEntity : ApplicationDataCenterObjectEntity, new()
    {
        var query = db.Queryable<TEntity>()
            .Where(item =>
                item.ModuleKey == moduleKey &&
                !item.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var published = await query.Where(item => item.Status == ApplicationDataCenterObjectStatus.Published).CountAsync(cancellationToken);
        var warning = await query.Where(item => item.Status == ApplicationDataCenterObjectStatus.Warning).CountAsync(cancellationToken);
        var error = await query.Where(item => item.Status == ApplicationDataCenterObjectStatus.Error).CountAsync(cancellationToken);
        return new ApplicationDataCenterModuleOverviewResponse(
            moduleKey,
            title,
            description,
            viewPermissionCode,
            total,
            published,
            warning,
            error);
    }

    private static ApplicationDataCenterObjectListItemResponse MapListItem<TEntity>(TEntity entity)
        where TEntity : ApplicationDataCenterObjectEntity
        => new(
            entity.Id,
            entity.ModuleKey,
            entity.ObjectCode,
            entity.ObjectName,
            entity.ObjectType,
            entity.Status,
            entity.VersionNo,
            entity.OwnerUserId,
            entity.OwnerName,
            entity.Environment,
            entity.Endpoint,
            entity.LastValidationStatus,
            entity.LastValidationMessage,
            entity.LastValidatedAt,
            0,
            entity.CreatedTime,
            entity.UpdatedTime,
            entity.Remark);

    private static ApplicationDataCenterObjectDetailResponse MapDetail<TEntity>(TEntity entity)
        where TEntity : ApplicationDataCenterObjectEntity
        => new(
            entity.Id,
            entity.ModuleKey,
            entity.ObjectCode,
            entity.ObjectName,
            entity.ObjectType,
            entity.Status,
            entity.VersionNo,
            entity.OwnerUserId,
            entity.OwnerName,
            entity.Environment,
            entity.Endpoint,
            entity.ConfigJson,
            entity.PublicConfigJson,
            entity.LastValidationStatus,
            entity.LastValidationMessage,
            entity.LastValidatedAt,
            new ApplicationDataCenterReferenceSummaryResponse(entity.ObjectType, entity.Id, 0, 0, 0, 0, 0, []),
            [],
            entity.CreatedTime,
            entity.UpdatedTime,
            entity.Remark);

    private static async Task<List<ApplicationDataCenterObjectListItemResponse>> LoadRecentItemsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var items = new List<ApplicationDataCenterObjectListItemResponse>();
        items.AddRange((await TakeRecentAsync<ApplicationDataSourceEntity>(db, workspace, cancellationToken)).Select(MapListItem));
        items.AddRange((await TakeRecentAsync<ApplicationConnectionCheckTaskEntity>(db, workspace, cancellationToken)).Select(MapListItem));
        items.AddRange((await TakeRecentAsync<ApplicationDataModelDesignEntity>(db, workspace, cancellationToken)).Select(MapListItem));
        items.AddRange((await TakeRecentAsync<ApplicationApiServiceEntity>(db, workspace, cancellationToken)).Select(MapListItem));
        items.AddRange((await TakeRecentAsync<ApplicationMicroflowEntity>(db, workspace, cancellationToken)).Select(MapListItem));
        items.AddRange((await TakeRecentAsync<ApplicationDataEntityDefinitionEntity>(db, workspace, cancellationToken)).Select(MapListItem));
        items.AddRange((await TakeRecentAsync<ApplicationQueryDatasetEntity>(db, workspace, cancellationToken)).Select(MapListItem));
        items.AddRange((await TakeRecentAsync<ApplicationIntegrationTaskEntity>(db, workspace, cancellationToken)).Select(MapListItem));
        items.AddRange((await TakeRecentAsync<ApplicationDataCenterDictionaryEntity>(db, workspace, cancellationToken)).Select(MapListItem));

        return items
            .OrderByDescending(item => item.UpdatedTime ?? item.CreatedTime)
            .ThenBy(item => item.ObjectName)
            .Take(10)
            .ToList();
    }

    private static Task<List<TEntity>> TakeRecentAsync<TEntity>(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        CancellationToken cancellationToken)
        where TEntity : ApplicationDataCenterObjectEntity, new()
        => db.Queryable<TEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(2)
            .ToListAsync(cancellationToken);
}
