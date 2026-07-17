using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Application.Auth;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationConsoleService(
    ISqlSugarClient db,
    ICurrentUser currentUser,
    ApplicationDatabaseBindingResolver bindingResolver,
    IApplicationDatabaseConnectionFactory connectionFactory,
    ApplicationDatabaseSchemaInitializer schemaInitializer,
    ApplicationDatabaseCapabilityReader capabilityReader,
    ApplicationWorkspaceUserResolver applicationWorkspaceUserResolver,
    ILogger<ApplicationConsoleService> logger) : IApplicationConsoleService
{
    public async Task<ApplicationConsoleSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var workspace = await LoadCurrentApplicationWorkspaceAsync(cancellationToken);
        var canManage = CanManageDatabaseBinding();
        var application = BuildApplicationResponse(workspace);
        var emptyCounts = BuildUnboundCounts();

        var resolution = bindingResolver.ResolveStatus(
            workspace.TenantAppConfigJson,
            workspace.TenantId,
            workspace.AppCode);
        if (resolution.Options is null)
        {
            return new ApplicationConsoleSummaryResponse(
                application,
                BuildDatabaseBindingStatus(null, false, false, canManage, resolution.Message ?? string.Empty, resolution.Status),
                BuildMetrics(emptyCounts),
                emptyCounts,
                [],
                [],
                BuildEntryTree(workspace, emptyCounts, CreateEmptyEntryCounts(), []),
                BuildDevelopmentShortcuts(workspace, emptyCounts, CreateEmptyEntryCounts(), []),
                [],
                BuildEmptyVersionContext(),
                BuildDraftSignals(emptyCounts, BuildEmptyVersionContext()));
        }

        ApplicationDatabaseBindingOptions? binding = resolution.Options;
        try
        {
            binding = bindingResolver.Resolve(workspace.TenantAppConfigJson, workspace.TenantId, workspace.AppCode);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Application database binding cannot be resolved for {TenantId}/{AppCode}", workspace.TenantId, workspace.AppCode);
            return new ApplicationConsoleSummaryResponse(
                application,
                BuildDatabaseBindingStatus(null, isBound: bindingResolver.HasBinding(workspace.TenantAppConfigJson), isReachable: false, canManage, ex.Message),
                BuildMetrics(emptyCounts),
                emptyCounts,
                [],
                [],
                BuildEntryTree(workspace, emptyCounts, CreateEmptyEntryCounts(), []),
                BuildDevelopmentShortcuts(workspace, emptyCounts, CreateEmptyEntryCounts(), []),
                [],
                BuildEmptyVersionContext(),
                BuildDraftSignals(emptyCounts, BuildEmptyVersionContext()));
        }

        if (binding is null)
        {
            return new ApplicationConsoleSummaryResponse(
                application,
                BuildDatabaseBindingStatus(null, isBound: false, isReachable: false, canManage, "未绑定应用数据库"),
                BuildMetrics(emptyCounts),
                emptyCounts,
                [],
                [],
                BuildEntryTree(workspace, emptyCounts, CreateEmptyEntryCounts(), []),
                BuildDevelopmentShortcuts(workspace, emptyCounts, CreateEmptyEntryCounts(), []),
                [],
                BuildEmptyVersionContext(),
                BuildDraftSignals(emptyCounts, BuildEmptyVersionContext()));
        }

        try
        {
            using var appDb = CreateDisposableClient(binding);
            await appDb.Client.Ado.GetIntAsync("SELECT 1");
            var counts = await capabilityReader.ReadCountsAsync(appDb.Client, workspace.TenantId, workspace.AppCode, cancellationToken);
            var entryCounts = await LoadEntryCountsAsync(appDb.Client, workspace, counts, cancellationToken);
            var recentPublishes = await capabilityReader.ReadRecentPublishesAsync(appDb.Client, workspace.TenantId, workspace.AppCode, cancellationToken);
            var recentAudits = await capabilityReader.ReadRecentAuditsAsync(appDb.Client, currentUser.GetAsterErpUserId(), cancellationToken);
            var recentDevelopmentItems = await LoadRecentDevelopmentItemsAsync(appDb.Client, workspace, cancellationToken);
            var versionContext = await LoadVersionContextAsync(appDb.Client, workspace, counts, recentPublishes, cancellationToken);
            var draftSignals = BuildDraftSignals(counts, versionContext);
            return new ApplicationConsoleSummaryResponse(
                application,
                BuildDatabaseBindingStatus(binding, isBound: true, isReachable: true, canManage, "应用数据库已绑定"),
                BuildMetrics(counts),
                counts,
                recentPublishes,
                recentAudits,
                BuildEntryTree(workspace, counts, entryCounts, recentDevelopmentItems),
                BuildDevelopmentShortcuts(workspace, counts, entryCounts, recentDevelopmentItems),
                recentDevelopmentItems,
                versionContext,
                draftSignals);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Application database summary failed for {TenantId}/{AppCode}", workspace.TenantId, workspace.AppCode);
            return new ApplicationConsoleSummaryResponse(
                application,
                BuildDatabaseBindingStatus(binding, isBound: true, isReachable: false, canManage, "应用数据库连接失败，请检查绑定配置"),
                BuildMetrics(emptyCounts),
                emptyCounts,
                [],
                [],
                BuildEntryTree(workspace, emptyCounts, CreateEmptyEntryCounts(), []),
                BuildDevelopmentShortcuts(workspace, emptyCounts, CreateEmptyEntryCounts(), []),
                [],
                BuildEmptyVersionContext(),
                BuildDraftSignals(emptyCounts, BuildEmptyVersionContext()));
        }
    }

    public async Task<ApplicationDatabaseBindingStatusResponse> GetDatabaseBindingStatusAsync(CancellationToken cancellationToken = default)
    {
        var workspace = await LoadCurrentApplicationWorkspaceAsync(cancellationToken);
        var canManage = CanManageDatabaseBinding();
        var resolution = bindingResolver.ResolveStatus(
            workspace.TenantAppConfigJson,
            workspace.TenantId,
            workspace.AppCode);
        if (resolution.Options is null)
        {
            return BuildDatabaseBindingStatus(null, false, false, canManage, resolution.Message ?? string.Empty, resolution.Status);
        }

        ApplicationDatabaseBindingOptions? binding = resolution.Options;
        try
        {
            binding = bindingResolver.Resolve(workspace.TenantAppConfigJson, workspace.TenantId, workspace.AppCode);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Application database binding status cannot be resolved for {TenantId}/{AppCode}", workspace.TenantId, workspace.AppCode);
            return BuildDatabaseBindingStatus(null, isBound: bindingResolver.HasBinding(workspace.TenantAppConfigJson), isReachable: false, canManage, ex.Message);
        }

        if (binding is null)
        {
            return BuildDatabaseBindingStatus(null, isBound: false, isReachable: false, canManage, "未绑定应用数据库");
        }

        try
        {
            using var appDb = CreateDisposableClient(binding);
            await appDb.Client.Ado.GetIntAsync("SELECT 1");
            return BuildDatabaseBindingStatus(binding, isBound: true, isReachable: true, canManage, "应用数据库已绑定");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Application database binding status failed for {TenantId}/{AppCode}", workspace.TenantId, workspace.AppCode);
            return BuildDatabaseBindingStatus(binding, isBound: true, isReachable: false, canManage, "应用数据库连接失败，请检查绑定配置");
        }
    }

    public async Task<ApplicationDatabaseBindingResponse> TestDatabaseBindingAsync(
        ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await LoadCurrentApplicationWorkspaceAsync(cancellationToken);
        EnsureCanManageDatabaseBinding();

        var options = bindingResolver.CreateOptions(request, workspace.TenantId, workspace.AppCode);
        await connectionFactory.ValidateAsync(options, cancellationToken);
        return BuildDatabaseBindingResponse(options, isReachable: true, "应用数据库连接成功");
    }

    public async Task<ApplicationDatabaseBindingResponse> SaveDatabaseBindingAsync(
        ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await LoadCurrentApplicationWorkspaceAsync(cancellationToken);
        EnsureCanManageDatabaseBinding();

        var options = bindingResolver.CreateOptions(request, workspace.TenantId, workspace.AppCode);
        await connectionFactory.ValidateAsync(options, cancellationToken);
        var currentUserEntity = await LoadCurrentUserEntityAsync(workspace, cancellationToken);

        using (var appDb = CreateDisposableClient(options))
        {
            await schemaInitializer.InitializeAsync(
                appDb.Client,
                workspace.TenantId,
                workspace.AppCode,
                currentUserEntity,
                cancellationToken,
                workspace.TenantAppConfigJson);
        }

        var updatedAt = DateTime.UtcNow;
        var updatedBy = currentUser.GetAsterErpUserId();
        var configJson = bindingResolver.Merge(workspace.TenantAppConfigJson, options, updatedBy, updatedAt);
        await db.Updateable<SystemTenantAppEntity>()
            .SetColumns(item => new SystemTenantAppEntity
            {
                ConfigJson = configJson,
                UpdatedBy = updatedBy,
                UpdatedTime = updatedAt
            })
            .Where(item => item.Id == workspace.TenantAppId && !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);

        return BuildDatabaseBindingResponse(options with { UpdatedAt = updatedAt, UpdatedBy = updatedBy }, isReachable: true, "应用数据库绑定已保存");
    }

    private async Task<ApplicationConsoleWorkspaceRow> LoadCurrentApplicationWorkspaceAsync(CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode()?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(appCode) ||
            string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("请先进入应用工作区", ErrorCodes.PermissionDenied);
        }

        return await LoadWorkspaceAsync(tenantId, appCode, cancellationToken);
    }

    private async Task<SystemUserEntity> LoadCurrentUserEntityAsync(
        ApplicationConsoleWorkspaceRow workspace,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetAsterErpUserId();
        var applicationUser = await applicationWorkspaceUserResolver.FindByIdAsync(
            userId,
            workspace.TenantAppConfigJson,
            workspace.TenantId,
            workspace.AppCode,
            cancellationToken);
        if (applicationUser is not null)
        {
            return applicationUser;
        }

        throw new ValidationException("当前用户不存在或已停用", ErrorCodes.AuthenticationRequired);
    }

    private async Task<ApplicationConsoleWorkspaceRow> LoadWorkspaceAsync(string tenantId, string appCode, CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
                (tenantApp, tenant, app) => tenantApp.TenantId == tenant.Id && tenantApp.AppCode == app.AppCode)
            .Where((tenantApp, tenant, app) =>
                tenantApp.TenantId == tenantId &&
                tenantApp.AppCode == appCode &&
                !tenantApp.IsDeleted &&
                !tenant.IsDeleted &&
                !app.IsDeleted)
            .Select((tenantApp, tenant, app) => new ApplicationConsoleWorkspaceRow
            {
                TenantAppId = tenantApp.Id,
                TenantId = tenant.Id,
                TenantName = tenant.TenantName,
                AppCode = app.AppCode,
                AppName = app.AppName,
                SystemName = tenantApp.SystemName,
                Version = app.Version,
                DefaultRoutePath = app.DefaultRoutePath,
                Status = app.Status,
                AppType = app.AppType,
                CreatedTime = app.CreatedTime,
                UpdatedTime = app.UpdatedTime,
                TenantAppConfigJson = tenantApp.ConfigJson
            })
            .Take(1)
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault()
            ?? throw new ValidationException("当前应用工作区不存在或已停用", ErrorCodes.PermissionDenied);
    }

    private static ApplicationConsoleCapabilityCountsResponse BuildUnboundCounts()
    {
        var fixedMenuCount = ApplicationShellMenuCatalog.Items.Count(item => item.MenuType != "Directory");
        return new ApplicationConsoleCapabilityCountsResponse(
            fixedMenuCount,
            fixedMenuCount,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
    }

    private static ApplicationConsoleApplicationResponse BuildApplicationResponse(ApplicationConsoleWorkspaceRow workspace) =>
        new(
            workspace.TenantId,
            workspace.TenantName,
            workspace.AppCode,
            workspace.AppName,
            string.IsNullOrWhiteSpace(workspace.SystemName)
                ? $"{workspace.TenantName} {workspace.AppName}"
                : workspace.SystemName,
            workspace.Version,
            workspace.DefaultRoutePath,
            workspace.Status,
            workspace.AppType,
            workspace.CreatedTime,
            workspace.UpdatedTime,
            "application");

    private IReadOnlyList<ApplicationConsoleEntryTreeGroupResponse> BuildEntryTree(
        ApplicationConsoleWorkspaceRow workspace,
        ApplicationConsoleCapabilityCountsResponse capabilityCounts,
        IReadOnlyDictionary<string, int> entryCounts,
        IReadOnlyList<ApplicationConsoleRecentDevelopmentItemResponse> recentDevelopmentItems)
    {
        var latestDesign = recentDevelopmentItems.FirstOrDefault();
        var developmentCenterItems = BuildEntryItems(
        [
            CreateEntryItem(
                key: "page-design",
                title: "页面设计",
                description: "进入完整低代码页面工作台，继续构建页面、菜单与发布链路。",
                icon: "files",
                routePath: BuildWorkspaceAdminRoute(workspace, "development-center/pages"),
                permissionCode: PermissionCodes.AppDevelopmentCenterBusinessObjectView,
                visitKind: "page-design",
                accent: "emerald",
                count: GetEntryCount(entryCounts, "page-design"),
                countLabel: "页",
                recentTargetTitle: latestDesign?.Title),
            CreateEntryItem(
                key: "page-design-designer",
                title: "最近设计页",
                description: latestDesign is null ? "没有最近设计页面，点击进入页面设计工作台。" : "继续上次最近编辑的页面设计器。",
                icon: "edit",
                routePath: latestDesign?.ContinueRoutePath ?? BuildWorkspaceAdminRoute(workspace, "development-center/pages"),
                permissionCode: PermissionCodes.AppDevelopmentCenterBusinessObjectView,
                visitKind: "designer",
                accent: "emerald",
                count: null,
                countLabel: null,
                recentTargetTitle: latestDesign?.Title),
            CreateEntryItem(
                key: "logic-microflow",
                title: "逻辑功能",
                description: "进入微流管理，维护逻辑编排、变量、查询与写入动作。",
                icon: "braces",
                routePath: BuildWorkspaceAdminRoute(workspace, "data-center/microflows"),
                permissionCode: PermissionCodes.AppDataCenterMicroflowView,
                visitKind: "microflow",
                accent: "emerald",
                count: GetEntryCount(entryCounts, "microflow-management"),
                countLabel: "条",
                recentTargetTitle: null),
            CreateEntryItem(
                key: "workflow-design",
                title: "流程设计",
                description: "进入流程模型管理，继续维护审批流程与流程设计。",
                icon: "gitBranch",
                routePath: BuildWorkspaceAdminRoute(workspace, "workflows/models"),
                permissionCode: PermissionCodes.WorkflowModelQuery,
                visitKind: "workflow-design",
                accent: "emerald",
                count: capabilityCounts.WorkflowModelCount,
                countLabel: "个",
                recentTargetTitle: null),
            CreateEntryItem(
                key: "asset-reuse",
                title: "资产复用",
                description: "进入共享资源列表，查看可复用模板、片段与公共资源。",
                icon: "package",
                routePath: BuildWorkspaceAdminRoute(workspace, "development-center/shared-resources"),
                permissionCode: PermissionCodes.AppDevelopmentCenterView,
                visitKind: "shared-resources",
                accent: "emerald",
                count: GetEntryCount(entryCounts, "asset-reuse"),
                countLabel: "项",
                recentTargetTitle: null)
        ]);

        var dataCenterItems = BuildEntryItems(
        [
            CreateEntryItem(
                key: "data-modeling",
                title: "数据建模",
                description: "进入实体与字段工作区，继续维护模型、字段与结构定义。",
                icon: "table",
                routePath: BuildWorkspaceAdminRoute(workspace, "data-center/entities-fields"),
                permissionCode: PermissionCodes.AppDataCenterEntityFieldView,
                visitKind: "data-modeling",
                accent: "purple",
                count: capabilityCounts.DataModelCount,
                countLabel: "个",
                recentTargetTitle: null),
            CreateEntryItem(
                key: "data-sources",
                title: "数据源",
                description: "进入数据源管理，查看数据库连接、工作台与表结构。",
                icon: "database",
                routePath: BuildWorkspaceAdminRoute(workspace, "data-center/data-sources"),
                permissionCode: PermissionCodes.AppDataCenterDataSourceView,
                visitKind: "data-sources",
                accent: "purple",
                count: GetEntryCount(entryCounts, "data-sources"),
                countLabel: "个",
                recentTargetTitle: null),
            CreateEntryItem(
                key: "query-datasets",
                title: "查询视图",
                description: "进入查询视图与数据集，管理报表视图与可复用查询结果。",
                icon: "activity",
                routePath: BuildWorkspaceAdminRoute(workspace, "data-center/query-datasets"),
                permissionCode: PermissionCodes.AppDataCenterQueryDatasetView,
                visitKind: "query-datasets",
                accent: "purple",
                count: GetEntryCount(entryCounts, "query-datasets"),
                countLabel: "项",
                recentTargetTitle: null),
            CreateEntryItem(
                key: "microflow-management",
                title: "微流管理",
                description: "进入微流管理，维护数据逻辑、输入输出与动作编排。",
                icon: "braces",
                routePath: BuildWorkspaceAdminRoute(workspace, "data-center/microflows"),
                permissionCode: PermissionCodes.AppDataCenterMicroflowView,
                visitKind: "microflow",
                accent: "purple",
                count: GetEntryCount(entryCounts, "microflow-management"),
                countLabel: "条",
                recentTargetTitle: null),
            CreateEntryItem(
                key: "integration-tasks",
                title: "同步集成",
                description: "进入数据同步与集成任务，查看任务编排、状态与日志。",
                icon: "refresh",
                routePath: BuildWorkspaceAdminRoute(workspace, "data-center/integration-tasks"),
                permissionCode: PermissionCodes.AppDataCenterIntegrationTaskView,
                visitKind: "integration-tasks",
                accent: "purple",
                count: GetEntryCount(entryCounts, "integration-tasks"),
                countLabel: "项",
                recentTargetTitle: null)
        ]);

        var groups = new List<ApplicationConsoleEntryTreeGroupResponse>();
        if (developmentCenterItems.Count > 0)
        {
            groups.Add(new ApplicationConsoleEntryTreeGroupResponse(
                "development-center",
                "开发中心",
                "围绕页面设计、逻辑功能、流程设计与资产复用进入真实工作区。",
                "code",
                developmentCenterItems));
        }

        if (dataCenterItems.Count > 0)
        {
            groups.Add(new ApplicationConsoleEntryTreeGroupResponse(
                "data-center",
                "数据中心",
                "围绕数据建模、数据源、查询视图、微流与集成任务进入承接页。",
                "database",
                dataCenterItems));
        }

        return groups;
    }

    private IReadOnlyList<ApplicationConsoleDevelopmentShortcutResponse> BuildDevelopmentShortcuts(
        ApplicationConsoleWorkspaceRow workspace,
        ApplicationConsoleCapabilityCountsResponse capabilityCounts,
        IReadOnlyDictionary<string, int> entryCounts,
        IReadOnlyList<ApplicationConsoleRecentDevelopmentItemResponse> recentDevelopmentItems)
    {
        var latestDesign = recentDevelopmentItems.FirstOrDefault();
        var shortcuts = new List<ApplicationConsoleDevelopmentShortcutResponse>();

        var pageDesign = CreateDevelopmentShortcut(
            key: "page-design",
            title: "进入页面设计",
            description: "直接进入完整低代码页面工作台，管理页面、菜单和预览发布。",
            icon: "files",
            routePath: BuildWorkspaceAdminRoute(workspace, "development-center/pages"),
            permissionCode: PermissionCodes.AppDevelopmentCenterBusinessObjectView,
            visitKind: "page-design",
            actionText: "进入工作台",
            accent: "emerald",
            count: GetEntryCount(entryCounts, "page-design"),
            countLabel: "页",
            recentTargetTitle: latestDesign?.Title);
        if (pageDesign is not null)
        {
            shortcuts.Add(pageDesign);
        }

        if (latestDesign is not null)
        {
            var recentDesigner = CreateDevelopmentShortcut(
                key: "recent-designer",
                title: "继续最近设计",
                description: "直接返回上次最近编辑的页面设计器。",
                icon: "edit",
                routePath: latestDesign.ContinueRoutePath,
                permissionCode: PermissionCodes.AppDevelopmentCenterBusinessObjectView,
                visitKind: "designer",
                actionText: "继续设计",
                accent: "emerald",
                count: null,
                countLabel: null,
                recentTargetTitle: latestDesign.Title);
            if (recentDesigner is not null)
            {
                shortcuts.Add(recentDesigner);
            }
        }

        var dataModeling = CreateDevelopmentShortcut(
            key: "data-modeling",
            title: "进入数据建模",
            description: "直接进入实体与字段承接页，继续维护模型、结构和数据定义。",
            icon: "table",
            routePath: BuildWorkspaceAdminRoute(workspace, "data-center/entities-fields"),
            permissionCode: PermissionCodes.AppDataCenterEntityFieldView,
            visitKind: "data-modeling",
            actionText: "进入建模",
            accent: "purple",
            count: capabilityCounts.DataModelCount,
            countLabel: "个",
            recentTargetTitle: null);
        if (dataModeling is not null)
        {
            shortcuts.Add(dataModeling);
        }

        var microflow = CreateDevelopmentShortcut(
            key: "microflow-management",
            title: "进入微流管理",
            description: "直接进入微流管理承接页，继续维护逻辑功能和数据流转。",
            icon: "braces",
            routePath: BuildWorkspaceAdminRoute(workspace, "data-center/microflows"),
            permissionCode: PermissionCodes.AppDataCenterMicroflowView,
            visitKind: "microflow",
            actionText: "进入微流",
            accent: "blue",
            count: GetEntryCount(entryCounts, "microflow-management"),
            countLabel: "条",
            recentTargetTitle: null);
        if (microflow is not null)
        {
            shortcuts.Add(microflow);
        }

        return shortcuts;
    }

    private async Task<IReadOnlyDictionary<string, int>> LoadEntryCountsAsync(
        ISqlSugarClient appDb,
        ApplicationConsoleWorkspaceRow workspace,
        ApplicationConsoleCapabilityCountsResponse capabilityCounts,
        CancellationToken cancellationToken)
    {
        var counts = CreateEmptyEntryCounts();
        counts["page-design"] = await SafeCountAsync(
            () => appDb.Queryable<ApplicationDevelopmentPageEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
                .CountAsync(cancellationToken),
            nameof(ApplicationDevelopmentPageEntity));
        counts["data-sources"] = await SafeCountAsync(
            () => appDb.Queryable<ApplicationDataSourceEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
                .CountAsync(cancellationToken),
            nameof(ApplicationDataSourceEntity));
        counts["microflow-management"] = await SafeCountAsync(
            () => appDb.Queryable<ApplicationMicroflowEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
                .CountAsync(cancellationToken),
            nameof(ApplicationMicroflowEntity));
        counts["query-datasets"] = await SafeCountAsync(
            () => appDb.Queryable<ApplicationQueryDatasetEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
                .CountAsync(cancellationToken),
            nameof(ApplicationQueryDatasetEntity));
        counts["integration-tasks"] = await SafeCountAsync(
            () => appDb.Queryable<ApplicationIntegrationTaskEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
                .CountAsync(cancellationToken),
            nameof(ApplicationIntegrationTaskEntity));
        counts["asset-reuse"] = await SafeCountAsync(
            () => appDb.Queryable<ApplicationSharedResourceEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
                .CountAsync(cancellationToken),
            nameof(ApplicationSharedResourceEntity));
        counts["data-modeling"] = capabilityCounts.DataModelCount;
        counts["workflow-design"] = capabilityCounts.WorkflowModelCount;
        return counts;
    }

    private async Task<IReadOnlyList<ApplicationConsoleRecentDevelopmentItemResponse>> LoadRecentDevelopmentItemsAsync(
        ISqlSugarClient appDb,
        ApplicationConsoleWorkspaceRow workspace,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasAsterErpPermission(PermissionCodes.AppDevelopmentCenterBusinessObjectView))
        {
            return [];
        }

        return await SafeListAsync(
            async () =>
            {
                var pages = await appDb.Queryable<ApplicationDevelopmentPageEntity>()
                    .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
                    .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                    .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                    .Take(8)
                    .ToListAsync(cancellationToken);
                if (pages.Count == 0)
                {
                    return [];
                }

                var moduleIds = pages
                    .Where(item => !string.IsNullOrWhiteSpace(item.ModuleId))
                    .Select(item => item.ModuleId!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var versionIds = pages
                    .Select(item => item.VersionId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var pageIds = pages
                    .Select(item => item.Id)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var modules = moduleIds.Length == 0
                    ? []
                    : await appDb.Queryable<ApplicationDevelopmentModuleEntity>()
                        .Where(item => moduleIds.Contains(item.Id) && !item.IsDeleted)
                        .ToListAsync(cancellationToken);
                var versions = versionIds.Length == 0
                    ? []
                    : await appDb.Queryable<ApplicationDevelopmentVersionEntity>()
                        .Where(item => versionIds.Contains(item.Id) && !item.IsDeleted)
                        .ToListAsync(cancellationToken);
                var moduleById = modules.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
                var versionById = versions.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
                var canContinueDesign = currentUser.HasAsterErpPermission(PermissionCodes.AppDevelopmentCenterBusinessObjectView);
                var canPreview = currentUser.HasAsterErpPermission(PermissionCodes.AppDevelopmentCenterDesignerPreview);
                var canPublish = currentUser.HasAsterErpPermission(PermissionCodes.AppDevelopmentCenterDesignerPublish);

                return pages
                    .OrderByDescending(item => item.UpdatedTime ?? item.CreatedTime)
                    .ThenByDescending(item => item.CreatedTime)
                    .Select(item =>
                    {
                        moduleById.TryGetValue(item.ModuleId ?? string.Empty, out var module);
                        versionById.TryGetValue(item.VersionId, out var version);
                        var routePath = BuildWorkspaceAdminRoute(
                            workspace,
                            $"development-center/pages/{Uri.EscapeDataString(item.Id)}/designer");
                        var previewRoutePath = !string.IsNullOrWhiteSpace(item.PublishedArtifactId)
                            ? BuildWorkspaceAdminRoute(workspace, BuildPreviewRouteForConsole(item, item.PageCode))
                            : null;
                        var descriptionParts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(module?.ModuleName))
                        {
                            descriptionParts.Add(module.ModuleName);
                        }

                        if (!string.IsNullOrWhiteSpace(version?.VersionName))
                        {
                            descriptionParts.Add(version.VersionName);
                        }

                        return new ApplicationConsoleRecentDevelopmentItemResponse(
                            item.Id,
                            item.Id,
                            item.PageName,
                            item.PageCode,
                            item.Status,
                            descriptionParts.Count == 0 ? "页面设计对象" : string.Join(" / ", descriptionParts),
                            module?.ModuleName,
                            module?.ModuleCode,
                            version?.Id,
                            version?.VersionName,
                            version?.VersionCode,
                            routePath,
                            previewRoutePath,
                            canContinueDesign,
                            canPreview && !string.IsNullOrWhiteSpace(previewRoutePath),
                            canPublish,
                            item.UpdatedTime ?? item.CreatedTime,
                            "designer");
                    })
                    .ToArray();
            },
            nameof(ApplicationDevelopmentPageEntity));
    }

    private async Task<ApplicationConsoleVersionContextResponse> LoadVersionContextAsync(
        ISqlSugarClient appDb,
        ApplicationConsoleWorkspaceRow workspace,
        ApplicationConsoleCapabilityCountsResponse capabilityCounts,
        IReadOnlyList<ApplicationConsoleRecentItemResponse> recentPublishes,
        CancellationToken cancellationToken)
    {
        var versions = await SafeQueryAsync(
            () => appDb.Queryable<ApplicationDevelopmentVersionEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .Take(20)
                .ToListAsync(cancellationToken),
            nameof(ApplicationDevelopmentVersionEntity));

        var latestDraft = versions
            .Where(item => string.Equals(item.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedTime ?? item.CreatedTime)
            .FirstOrDefault();
        var latestPublished = versions
            .Where(item => string.Equals(item.Status, "Published", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedTime ?? item.CreatedTime)
            .FirstOrDefault();
        var latestPublishTime = recentPublishes
            .OrderByDescending(item => item.CreatedTime)
            .Select(item => (DateTime?)item.CreatedTime)
            .FirstOrDefault();
        var latestDraftSnapshot = latestDraft is null
            ? null
            : new ApplicationConsoleVersionSnapshotResponse(
                latestDraft.Id,
                latestDraft.VersionName,
                latestDraft.VersionCode,
                latestDraft.Status,
                latestDraft.UpdatedTime ?? latestDraft.CreatedTime);
        var latestPublishedSnapshot = latestPublished is null
            ? null
            : new ApplicationConsoleVersionSnapshotResponse(
                latestPublished.Id,
                latestPublished.VersionName,
                latestPublished.VersionCode,
                latestPublished.Status,
                latestPublished.UpdatedTime ?? latestPublished.CreatedTime);
        var summary = capabilityCounts.DraftVersionCount > 0
            ? $"存在 {capabilityCounts.DraftVersionCount} 个草稿版本待发布"
            : latestPublishedSnapshot is null
                ? "暂无已发布开发版本"
                : $"最近已发布版本：{latestPublishedSnapshot.VersionName} / {latestPublishedSnapshot.VersionCode}";
        return new ApplicationConsoleVersionContextResponse(
            capabilityCounts.DraftVersionCount,
            capabilityCounts.PublishedVersionCount,
            latestDraftSnapshot,
            latestPublishedSnapshot,
            latestPublishTime,
            summary);
    }

    private static ApplicationConsoleVersionContextResponse BuildEmptyVersionContext() =>
        new(
            0,
            0,
            null,
            null,
            null,
            "暂无可用开发版本");

    private static ApplicationConsoleDraftSignalsResponse BuildDraftSignals(
        ApplicationConsoleCapabilityCountsResponse counts,
        ApplicationConsoleVersionContextResponse versionContext)
    {
        var items = new List<ApplicationConsoleDraftSignalResponse>();
        if (counts.DraftPageCount > 0)
        {
            items.Add(new ApplicationConsoleDraftSignalResponse(
                "draft-pages",
                "草稿页面待发布",
                $"当前存在 {counts.DraftPageCount} 个草稿页面，仍未进入正式发布链路。",
                "warning",
                counts.DraftPageCount));
        }

        if (counts.DraftVersionCount > 0)
        {
            items.Add(new ApplicationConsoleDraftSignalResponse(
                "draft-versions",
                "草稿版本待处理",
                versionContext.LatestDraftVersion is null
                    ? $"当前存在 {counts.DraftVersionCount} 个草稿版本。"
                    : $"最近草稿版本：{versionContext.LatestDraftVersion.VersionName} / {versionContext.LatestDraftVersion.VersionCode}。",
                "info",
                counts.DraftVersionCount));
        }

        if (counts.PreviewMenuCount > 0)
        {
            items.Add(new ApplicationConsoleDraftSignalResponse(
                "preview-menus",
                "预览入口已生成",
                $"当前保留 {counts.PreviewMenuCount} 个预览菜单入口，建议核对后及时完成正式发布。",
                "muted",
                counts.PreviewMenuCount));
        }

        var totalRiskCount = items.Sum(item => item.Count);
        return new ApplicationConsoleDraftSignalsResponse(
            totalRiskCount,
            counts.DraftPageCount > 0 || counts.DraftVersionCount > 0,
            items);
    }

    private ApplicationConsoleEntryTreeItemResponse? CreateEntryItem(
        string key,
        string title,
        string description,
        string icon,
        string routePath,
        string permissionCode,
        string visitKind,
        string accent,
        int? count,
        string? countLabel,
        string? recentTargetTitle)
    {
        if (!currentUser.HasAsterErpPermission(permissionCode))
        {
            return null;
        }

        return new ApplicationConsoleEntryTreeItemResponse(
            key,
            title,
            description,
            icon,
            routePath,
            permissionCode,
            visitKind,
            accent,
            count,
            countLabel,
            recentTargetTitle);
    }

    private ApplicationConsoleDevelopmentShortcutResponse? CreateDevelopmentShortcut(
        string key,
        string title,
        string description,
        string icon,
        string routePath,
        string permissionCode,
        string visitKind,
        string actionText,
        string accent,
        int? count,
        string? countLabel,
        string? recentTargetTitle)
    {
        if (!currentUser.HasAsterErpPermission(permissionCode))
        {
            return null;
        }

        return new ApplicationConsoleDevelopmentShortcutResponse(
            key,
            title,
            description,
            icon,
            routePath,
            permissionCode,
            visitKind,
            actionText,
            accent,
            count,
            countLabel,
            recentTargetTitle);
    }

    private static IReadOnlyList<ApplicationConsoleEntryTreeItemResponse> BuildEntryItems(
        IEnumerable<ApplicationConsoleEntryTreeItemResponse?> items) =>
        items
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();

    private static Dictionary<string, int> CreateEmptyEntryCounts() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["page-design"] = 0,
            ["data-modeling"] = 0,
            ["data-sources"] = 0,
            ["microflow-management"] = 0,
            ["workflow-design"] = 0,
            ["query-datasets"] = 0,
            ["integration-tasks"] = 0,
            ["asset-reuse"] = 0
        };

    private static int GetEntryCount(IReadOnlyDictionary<string, int> counts, string key) =>
        counts.TryGetValue(key, out var count) ? count : 0;

    private static string BuildWorkspaceAdminRoute(ApplicationConsoleWorkspaceRow workspace, string routePath)
    {
        var prefix = $"/tenants/{Uri.EscapeDataString(workspace.TenantId)}/apps/{Uri.EscapeDataString(workspace.AppCode.ToUpperInvariant())}/admin";
        if (string.IsNullOrWhiteSpace(routePath))
        {
            return prefix;
        }

        return routePath.StartsWith("/", StringComparison.Ordinal)
            ? $"{prefix}{routePath}"
            : $"{prefix}/{routePath}";
    }

    private static string BuildPreviewRouteForConsole(ApplicationDevelopmentPageEntity page, string? pageCode = null) =>
        $"/pages/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(pageCode) ? page.PageCode : pageCode)}?previewPageId={Uri.EscapeDataString(page.Id)}";

    private async Task<int> SafeCountAsync(Func<Task<int>> countAsync, string source)
    {
        try
        {
            return await countAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Application console entry count skipped for {Source}", source);
            return 0;
        }
    }

    private async Task<IReadOnlyList<T>> SafeQueryAsync<T>(Func<Task<List<T>>> queryAsync, string source)
    {
        try
        {
            return await queryAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Application console query skipped for {Source}", source);
            return [];
        }
    }

    private async Task<IReadOnlyList<ApplicationConsoleRecentDevelopmentItemResponse>> SafeListAsync(
        Func<Task<IReadOnlyList<ApplicationConsoleRecentDevelopmentItemResponse>>> listAsync,
        string source)
    {
        try
        {
            return await listAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Application console recent development list skipped for {Source}", source);
            return [];
        }
    }

    private static IReadOnlyList<ApplicationConsoleMetricResponse> BuildMetrics(ApplicationConsoleCapabilityCountsResponse counts) =>
    [
        new("menus", "菜单数", counts.MenuCount.ToString(), "项", counts.MenuCount > 0 ? "ok" : "empty"),
        new("pages", "页面数", counts.PageCount.ToString(), "个", counts.PageCount > 0 ? "ok" : "empty"),
        new("models", "数据模型", counts.DataModelCount.ToString(), "个", counts.DataModelCount > 0 ? "ok" : "empty"),
        new("draftPages", "草稿页", counts.DraftPageCount.ToString(), "个", counts.DraftPageCount > 0 ? "ok" : "empty"),
        new("previewMenus", "预览菜单", counts.PreviewMenuCount.ToString(), "项", counts.PreviewMenuCount > 0 ? "ok" : "empty"),
        new("draftVersions", "草稿版本", counts.DraftVersionCount.ToString(), "个", counts.DraftVersionCount > 0 ? "ok" : "empty"),
        new("workflows", "流程模型", counts.WorkflowModelCount.ToString(), "个", counts.WorkflowModelCount > 0 ? "ok" : "empty"),
        new("publishTasks", "发布任务", counts.PublishTaskCount.ToString(), "次", counts.PublishTaskCount > 0 ? "ok" : "empty")
    ];

    private static ApplicationDatabaseBindingStatusResponse BuildDatabaseBindingStatus(
        ApplicationDatabaseBindingOptions? binding,
        bool isBound,
        bool isReachable,
        bool canManage,
        string message,
        string? status = null) =>
        new(
            isBound,
            isReachable,
            binding?.Provider,
            binding?.DisplayName,
            binding?.DatabaseName,
            binding?.UpdatedAt,
            canManage,
            message,
            status ?? (isReachable
                ? ApplicationDatabaseBindingStatus.Ready
                : isBound
                    ? ApplicationDatabaseBindingStatus.Unavailable
                    : ApplicationDatabaseBindingStatus.NotConfigured));

    private ApplicationDatabaseBindingResponse BuildDatabaseBindingResponse(
        ApplicationDatabaseBindingOptions binding,
        bool isReachable,
        string message) =>
        new(
            true,
            isReachable,
            binding.Provider,
            binding.DisplayName,
            binding.DatabaseName,
            binding.UpdatedAt,
            CanManageDatabaseBinding(),
            message,
            ApplicationDatabaseBindingStatus.Ready);

    private bool CanManageDatabaseBinding() =>
        currentUser.IsAsterErpTenantAdmin() ||
        currentUser.HasAsterErpPermission("*");

    private void EnsureCanManageDatabaseBinding()
    {
        if (!CanManageDatabaseBinding())
        {
            throw new ValidationException("你暂无应用数据库绑定权限", ErrorCodes.PermissionDenied);
        }
    }

    private DisposableApplicationDb CreateDisposableClient(ApplicationDatabaseBindingOptions binding)
    {
        var client = connectionFactory.Create(binding);
        return new DisposableApplicationDb(client);
    }

    private sealed class DisposableApplicationDb(ISqlSugarClient client) : IDisposable
    {
        public ISqlSugarClient Client { get; } = client;

        public void Dispose()
        {
            if (Client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
