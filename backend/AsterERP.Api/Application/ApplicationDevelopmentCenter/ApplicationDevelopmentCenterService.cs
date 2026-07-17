using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentCenterService(
    ISqlSugarClient mainDb,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDevelopmentSchemaCompiler schemaCompiler,
    ApplicationDevelopmentSchemaValidator schemaValidator,
    ApplicationPageRuntimeEnvironmentCheckService runtimeEnvironmentCheckService,
    ApplicationDevelopmentPageRevisionGuard pageRevisionGuard,
    ApplicationDesignerDocumentStore documentStore,
    ApplicationDesignerArtifactPublisher artifactPublisher,
    ICurrentUser currentUser)
{
    internal const string LatestDesignerMode = "structured";
    private const string DefaultPreviewParentCode = "dev-center";
    private static readonly HashSet<string> DeprecatedDesignerActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "queryModel",
        "submitForm",
        "deleteModelRow",
        "deleteRow",
        "queryCompositeDetail",
        "submitCompositeForm",
        "deleteComposite",
        "loadCompositeDetail"
    };

    private static readonly HashSet<string> DeprecatedDesignerDatasetSourceKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "modelQuery",
        "modelOperation"
    };

    private static readonly string[] DeprecatedDesignerDataBindingKeys =
    [
        "modelCode",
        "saveModelCode",
        "queryModelCode",
        "rootModelCode",
        "dataSourceId",
        "tableName"
    ];

    public async Task<ApplicationDevelopmentOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        return new ApplicationDevelopmentOverviewResponse
        {
            DraftPageCount = await db.Queryable<ApplicationDevelopmentPageEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted && item.Status == "Draft")
                .CountAsync(cancellationToken),
            DraftVersionCount = await db.Queryable<ApplicationDevelopmentVersionEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted && item.Status == "Draft")
                .CountAsync(cancellationToken),
            PreviewMenuCount = await db.Queryable<SystemMenuEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted && item.ScopeType == "ApplicationDraftPreview")
                .CountAsync(cancellationToken),
            PublishedPageCount = await db.Queryable<ApplicationDevelopmentPageEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted && item.Status == "Published")
                .CountAsync(cancellationToken),
            PublishedVersionCount = await db.Queryable<ApplicationDevelopmentVersionEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted && item.Status == "Published")
                .CountAsync(cancellationToken),
            TotalModuleCount = await db.Queryable<ApplicationDevelopmentModuleEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
                .CountAsync(cancellationToken)
        };
    }

    public async Task<ApplicationDevelopmentAppConfigResponse> GetAppConfigAsync(CancellationToken cancellationToken = default)
    {
        var tenantApp = await GetTenantAppAsync(cancellationToken);
        var config = ApplicationDataCenterJson.DeserializeDictionary(tenantApp.ConfigJson);
        return new ApplicationDevelopmentAppConfigResponse
        {
            DefaultDataSourceId = ReadConfigString(config, "development.defaultDataSourceId"),
            Description = ReadConfigString(config, "development.description"),
            LogoIcon = ReadConfigString(config, "development.logoIcon"),
            PrimaryColor = ReadConfigString(config, "development.primaryColor") ?? tenantApp.PrimaryColor,
            SqlProtectionEnabled = ReadConfigBool(config, "development.sqlProtectionEnabled"),
            SystemFullName = ReadConfigString(config, "development.systemFullName") ?? tenantApp.SystemName,
            SystemShortName = ReadConfigString(config, "development.systemShortName")
        };
    }

    public async Task<ApplicationDevelopmentAppConfigResponse> SaveAppConfigAsync(
        ApplicationDevelopmentAppConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var tenantApp = await GetTenantAppAsync(cancellationToken);
        var config = ApplicationDataCenterJson.DeserializeDictionary(tenantApp.ConfigJson);
        SetConfigValue(config, "development.defaultDataSourceId", request.DefaultDataSourceId);
        SetConfigValue(config, "development.description", request.Description);
        SetConfigValue(config, "development.logoIcon", request.LogoIcon);
        SetConfigValue(config, "development.primaryColor", request.PrimaryColor);
        SetConfigValue(config, "development.systemFullName", request.SystemFullName);
        SetConfigValue(config, "development.systemShortName", request.SystemShortName);
        config["development.sqlProtectionEnabled"] = request.SqlProtectionEnabled;
        tenantApp.SystemName = ApplicationDataCenterCodePolicy.NormalizeOptional(request.SystemFullName, 128);
        tenantApp.PrimaryColor = ApplicationDataCenterCodePolicy.NormalizeOptional(request.PrimaryColor, 32);
        tenantApp.ConfigJson = ApplicationDataCenterJson.Serialize(config);
        tenantApp.UpdatedBy = workspace.UserId;
        tenantApp.UpdatedTime = DateTime.UtcNow;
        await mainDb.Updateable(tenantApp).ExecuteCommandAsync(cancellationToken);
        return await GetAppConfigAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationDevelopmentVersionDto>> GetVersionsAsync(CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var items = await db.Queryable<ApplicationDevelopmentVersionEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return items.Select(MapVersion).ToArray();
    }

    public async Task<ApplicationDevelopmentWorkspaceResponse> GetWorkspaceAsync(
        string? versionId,
        CancellationToken cancellationToken = default)
    {
        var overview = await GetOverviewAsync(cancellationToken);
        var versions = await GetVersionsAsync(cancellationToken);
        var selectedVersionId = ResolveWorkspaceVersionId(versions, versionId);
        var pages = string.IsNullOrWhiteSpace(selectedVersionId)
            ? []
            : (await GetPagesAsync(selectedVersionId, null, cancellationToken)).ToList();
        return new ApplicationDevelopmentWorkspaceResponse
        {
            Overview = overview,
            SelectedVersionId = selectedVersionId,
            SelectedVersion = versions.FirstOrDefault(item => item.Id == selectedVersionId),
            Versions = versions.ToList(),
            Modules = string.IsNullOrWhiteSpace(selectedVersionId)
                ? []
                : (await GetModulesAsync(selectedVersionId, cancellationToken)).ToList(),
            Pages = pages,
            SharedResources = string.IsNullOrWhiteSpace(selectedVersionId)
                ? []
                : (await GetSharedResourcesAsync(selectedVersionId, cancellationToken)).ToList(),
            RecentPages = pages
                .OrderByDescending(item => item.UpdatedTime ?? item.CreatedTime)
                .ThenBy(item => item.PageName)
                .Take(6)
                .ToList()
        };
    }

    public async Task<ApplicationDevelopmentVersionDto> CreateVersionAsync(
        ApplicationDevelopmentVersionUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var versionCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.VersionCode, "版本编码");
        await EnsureUniqueVersionCodeAsync(db, workspace, versionCode, null, cancellationToken);
        var entity = new ApplicationDevelopmentVersionEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            VersionCode = versionCode,
            VersionName = ApplicationDataCenterCodePolicy.NormalizeName(request.VersionName, "版本名称"),
            Status = NormalizeVersionStatus(request.Status),
            SourceDataSourceId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.SourceDataSourceId),
            DefaultPageId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.DefaultPageId),
            Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000),
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapVersion(entity);
    }

    public async Task<ApplicationDevelopmentVersionDto> UpdateVersionAsync(
        string id,
        ApplicationDevelopmentVersionUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await GetRequiredAsync<ApplicationDevelopmentVersionEntity>(db, workspace, id, cancellationToken);
        var versionCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.VersionCode, "版本编码");
        await EnsureUniqueVersionCodeAsync(db, workspace, versionCode, id, cancellationToken);
        entity.VersionCode = versionCode;
        entity.VersionName = ApplicationDataCenterCodePolicy.NormalizeName(request.VersionName, "版本名称");
        entity.Status = NormalizeVersionStatus(request.Status);
        entity.SourceDataSourceId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.SourceDataSourceId);
        entity.DefaultPageId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.DefaultPageId);
        entity.Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000);
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return MapVersion(entity);
    }

    private static string? ResolveWorkspaceVersionId(
        IReadOnlyList<ApplicationDevelopmentVersionDto> versions,
        string? requestedVersionId)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersionId) &&
            versions.Any(item => string.Equals(item.Id, requestedVersionId, StringComparison.OrdinalIgnoreCase)))
        {
            return requestedVersionId.Trim();
        }

        return versions
            .OrderByDescending(item => string.Equals(item.Status, "Published", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.UpdatedTime ?? item.CreatedTime)
            .Select(item => item.Id)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<ApplicationDevelopmentModuleTreeNodeDto>> GetModulesAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await GetRequiredAsync<ApplicationDevelopmentVersionEntity>(db, workspace, versionId, cancellationToken);
        var modules = await db.Queryable<ApplicationDevelopmentModuleEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.VersionId == versionId && !item.IsDeleted)
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var pageCounts = await db.Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.VersionId == versionId && !item.IsDeleted)
            .GroupBy(item => item.ModuleId)
            .Select(item => new { ModuleId = item.ModuleId, Count = SqlFunc.AggregateCount(item.Id) })
            .ToListAsync(cancellationToken);
        var countByModule = pageCounts.ToDictionary(item => item.ModuleId ?? string.Empty, item => item.Count, StringComparer.OrdinalIgnoreCase);
        var nodeById = modules.ToDictionary(
            item => item.Id,
            item => new ApplicationDevelopmentModuleTreeNodeDto
            {
                Id = item.Id,
                ModuleCode = item.ModuleCode,
                ModuleName = item.ModuleName,
                ParentModuleId = item.ParentModuleId,
                PageCount = countByModule.TryGetValue(item.Id, out var count) ? count : 0,
                SortOrder = item.SortOrder,
                VersionId = item.VersionId
            },
            StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodeById.Values)
        {
            if (!string.IsNullOrWhiteSpace(node.ParentModuleId) &&
                nodeById.TryGetValue(node.ParentModuleId, out var parent))
            {
                parent.Children.Add(node);
            }
        }

        return nodeById.Values
            .Where(item => string.IsNullOrWhiteSpace(item.ParentModuleId) || !nodeById.ContainsKey(item.ParentModuleId))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.ModuleName)
            .ToArray();
    }

    public async Task<ApplicationDevelopmentModuleTreeNodeDto> CreateModuleAsync(
        ApplicationDevelopmentModuleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await GetRequiredAsync<ApplicationDevelopmentVersionEntity>(db, workspace, request.VersionId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ParentModuleId))
        {
            await GetRequiredAsync<ApplicationDevelopmentModuleEntity>(db, workspace, request.ParentModuleId, cancellationToken);
        }

        var moduleCode = string.IsNullOrWhiteSpace(request.ModuleCode)
            ? await GenerateModuleCodeAsync(db, workspace, request.VersionId, cancellationToken)
            : ApplicationDataCenterCodePolicy.NormalizeCode(request.ModuleCode, "模块编码");
        await EnsureUniqueModuleCodeAsync(db, workspace, request.VersionId, moduleCode, null, cancellationToken);
        var entity = new ApplicationDevelopmentModuleEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            VersionId = request.VersionId,
            ModuleCode = moduleCode,
            ModuleName = ApplicationDataCenterCodePolicy.NormalizeName(request.ModuleName, "模块名称"),
            ParentModuleId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.ParentModuleId),
            SortOrder = request.SortOrder,
            Status = "Draft",
            Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000),
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await SyncApplicationRuntimeModuleSubtreeAsync(db, workspace, entity, null, cancellationToken);
        return new ApplicationDevelopmentModuleTreeNodeDto
        {
            Id = entity.Id,
            ModuleCode = entity.ModuleCode,
            ModuleName = entity.ModuleName,
            ParentModuleId = entity.ParentModuleId,
            PageCount = 0,
            SortOrder = entity.SortOrder,
            VersionId = entity.VersionId
        };
    }

    public async Task<ApplicationDevelopmentModuleTreeNodeDto> UpdateModuleAsync(
        string id,
        ApplicationDevelopmentModuleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await GetRequiredAsync<ApplicationDevelopmentModuleEntity>(db, workspace, id, cancellationToken);
        var originalModuleCode = entity.ModuleCode;
        var moduleCode = string.IsNullOrWhiteSpace(request.ModuleCode)
            ? entity.ModuleCode
            : ApplicationDataCenterCodePolicy.NormalizeCode(request.ModuleCode, "模块编码");
        await EnsureUniqueModuleCodeAsync(db, workspace, entity.VersionId, moduleCode, id, cancellationToken);
        entity.ModuleCode = moduleCode;
        entity.ModuleName = ApplicationDataCenterCodePolicy.NormalizeName(request.ModuleName, "模块名称");
        entity.ParentModuleId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.ParentModuleId);
        entity.SortOrder = request.SortOrder;
        entity.Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000);
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await SyncApplicationRuntimeModuleSubtreeAsync(db, workspace, entity, originalModuleCode, cancellationToken);
        return new ApplicationDevelopmentModuleTreeNodeDto
        {
            Id = entity.Id,
            ModuleCode = entity.ModuleCode,
            ModuleName = entity.ModuleName,
            ParentModuleId = entity.ParentModuleId,
            PageCount = await db.Queryable<ApplicationDevelopmentPageEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.ModuleId == entity.Id && !item.IsDeleted)
                .CountAsync(cancellationToken),
            SortOrder = entity.SortOrder,
            VersionId = entity.VersionId
        };
    }

    public async Task<bool> DeleteModuleAsync(string id, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await GetRequiredAsync<ApplicationDevelopmentModuleEntity>(db, workspace, id, cancellationToken);
        var childCount = await db.Queryable<ApplicationDevelopmentModuleEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.ParentModuleId == id && !item.IsDeleted)
            .CountAsync(cancellationToken);
        var pageCount = await db.Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.ModuleId == id && !item.IsDeleted)
            .CountAsync(cancellationToken);
        if (childCount > 0 || pageCount > 0)
        {
            throw new ValidationException(
                $"菜单仍被引用，无法删除：子菜单 {childCount} 个，页面 {pageCount} 个",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        entity.IsDeleted = true;
        entity.DeletedBy = workspace.UserId;
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await SoftDeleteApplicationRuntimeModuleMenuAsync(db, workspace, entity, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ApplicationDevelopmentPageListItemDto>> GetPagesAsync(
        string versionId,
        string? moduleId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var query = db.Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.VersionId == versionId && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            query = query.Where(item => item.ModuleId == moduleId);
        }

        var items = await query
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var publishedArtifacts = await LoadPublishedPageArtifactsAsync(db, workspace, items, cancellationToken);
        return items
            .Select(item => MapPageListItem(item, publishedArtifacts.GetValueOrDefault(item.Id)))
            .ToArray();
    }

    public async Task<ApplicationDevelopmentPageDetailDto> GetPageAsync(string id, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        return await MapPageDetailAsync(db, workspace, await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, id, cancellationToken), cancellationToken);
    }

    public async Task<ApplicationDevelopmentPageDetailDto> CreatePageAsync(
        ApplicationDevelopmentPageCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await GetRequiredAsync<ApplicationDevelopmentVersionEntity>(db, workspace, request.VersionId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ModuleId))
        {
            await GetRequiredAsync<ApplicationDevelopmentModuleEntity>(db, workspace, request.ModuleId, cancellationToken);
        }

        var parentPageId = await NormalizeParentPageIdAsync(db, workspace, request.VersionId, request.ParentPageId, null, cancellationToken);
        var pageName = ApplicationDataCenterCodePolicy.NormalizeName(request.PageName, "页面名称");
        var pageCode = string.IsNullOrWhiteSpace(request.PageCode)
            ? await ApplicationDevelopmentPageCodeGenerator.GenerateAsync(db, workspace, pageName, cancellationToken)
            : ApplicationDataCenterCodePolicy.NormalizeCode(request.PageCode, "页面编码");
        await EnsureUniquePageCodeAsync(db, workspace, pageCode, null, cancellationToken);
        var pageType = NormalizePageType(request.PageType);
        var pageParameters = NormalizePageParameters(request.PageParameters);
        var pageParametersJson = ApplicationDataCenterJson.Serialize(pageParameters);
        var documentJson = ApplicationDevelopmentPageDraftDocumentFactory.Create(pageCode, pageName, pageType, pageParameters);
        var entity = new ApplicationDevelopmentPageEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            VersionId = request.VersionId,
            ModuleId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.ModuleId),
            PageCode = pageCode,
            PageName = pageName,
            ParentPageId = parentPageId,
            PageParametersJson = pageParametersJson,
            PageType = pageType,
            TemplateCode = "designer-document",
            DesignerMode = LatestDesignerMode,
            PermissionConfigJson = "{}",
            SortOrder = request.SortOrder,
            Status = "Draft",
            Remark = null,
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        await db.Ado.BeginTranAsync();
        try
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await documentStore.SaveAsync(db, workspace, entity.Id, entity.VersionId, documentJson, null, null, "{\"type\":\"page-created\"}", cancellationToken);
            await UpsertPreviewMenuAsync(db, workspace, entity, cancellationToken);
            await db.Ado.CommitTranAsync();
        }
        catch
        {
            await db.Ado.RollbackTranAsync();
            throw;
        }
        return await MapPageDetailAsync(db, workspace, entity, cancellationToken);
    }

    public async Task<ApplicationDevelopmentPageDetailDto> UpdatePageAsync(
        string id,
        ApplicationDevelopmentPageUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, id, cancellationToken);
        pageRevisionGuard.EnsureCurrent(request.ExpectedUpdatedTime, entity.UpdatedTime);
        var pageCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.PageCode, "页面编码");
        await EnsureUniquePageCodeAsync(db, workspace, pageCode, id, cancellationToken);
        var permissionConfigJson = NormalizePermissionConfigJson(request.PermissionConfigJson);
        EnsurePermissionConfigurationAccess(entity.PermissionConfigJson, permissionConfigJson);
        var parentPageId = await NormalizeParentPageIdAsync(db, workspace, entity.VersionId, request.ParentPageId, id, cancellationToken);
        var documentJson = NormalizeLayoutJson(request.DocumentJson);
        var documentMetadata = ReadDocumentPageMetadata(documentJson);
        var pageType = documentMetadata.PageType;
        var pageParameters = documentMetadata.Parameters;
        var pageParametersJson = ApplicationDataCenterJson.Serialize(pageParameters);
        schemaValidator.ValidateDraft(documentJson);
        ValidateNoDeprecatedModelDirectBinding(documentJson);
        entity.ModuleId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.ModuleId);
        entity.PageCode = pageCode;
        entity.PageName = ApplicationDataCenterCodePolicy.NormalizeName(request.PageName, "页面名称");
        entity.ParentPageId = parentPageId;
        entity.PageParametersJson = pageParametersJson;
        entity.PageType = pageType;
        entity.TemplateCode = NormalizeTemplateCode(request.TemplateCode);
        entity.DesignerMode = NormalizeDesignerMode(request.DesignerMode);
        entity.PermissionConfigJson = permissionConfigJson;
        entity.SortOrder = request.SortOrder;
        entity.Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000);
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Ado.BeginTranAsync();
        try
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            var existingDocument = await documentStore.FindDocumentAsync(db, workspace, entity.Id, cancellationToken);
            await documentStore.SaveAsync(
                db,
                workspace,
                entity.Id,
                entity.VersionId,
                documentJson,
                existingDocument?.DocumentHash,
                null,
                "{\"type\":\"page-updated\"}",
                cancellationToken);
            await UpsertPreviewMenuAsync(db, workspace, entity, cancellationToken);
            await db.Ado.CommitTranAsync();
        }
        catch
        {
            await db.Ado.RollbackTranAsync();
            throw;
        }
        return await MapPageDetailAsync(db, workspace, entity, cancellationToken);
    }

    public async Task<bool> DeletePageAsync(string id, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await ExecuteInTransactionAsync(db, async () =>
        {
            var page = await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, id, cancellationToken);
            var childCount = await db.Queryable<ApplicationDevelopmentPageEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.ParentPageId == page.Id && !item.IsDeleted)
                .CountAsync(cancellationToken);
            if (childCount > 0)
            {
                throw new ValidationException(
                    $"页面仍被引用，无法删除：子页面 {childCount} 个",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var businessObjectCount = await db.Queryable<ApplicationBusinessObjectDesignEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.PageId == page.Id && !item.IsDeleted)
                .CountAsync(cancellationToken);
            if (businessObjectCount > 0)
            {
                throw new ValidationException(
                    "该页面由业务对象管理，请先从业务对象工作台删除",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var publishedArtifactId = page.PublishedArtifactId;
            var now = DateTime.UtcNow;
            page.IsDeleted = true;
            page.DeletedBy = workspace.UserId;
            page.DeletedTime = now;
            page.UpdatedBy = workspace.UserId;
            page.UpdatedTime = now;
            page.Status = "Deleted";
            page.PublishedArtifactId = null;
            page.PublishedMenuId = null;
            page.PublishedMenuCode = null;
            await db.Updateable(page).ExecuteCommandAsync(cancellationToken);
            await InvalidateDeletedPageRuntimeAsync(db, workspace, page, publishedArtifactId, now, cancellationToken);

            var version = await GetRequiredAsync<ApplicationDevelopmentVersionEntity>(db, workspace, page.VersionId, cancellationToken);
            if (string.Equals(version.DefaultPageId, page.Id, StringComparison.OrdinalIgnoreCase))
            {
                var remainingPages = await db.Queryable<ApplicationDevelopmentPageEntity>()
                    .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.VersionId == page.VersionId && !item.IsDeleted)
                    .OrderBy(item => item.SortOrder, OrderByType.Asc)
                    .OrderBy(item => item.CreatedTime, OrderByType.Asc)
                    .ToListAsync(cancellationToken);
                var replacement = remainingPages.FirstOrDefault(item => string.Equals(item.Status, "Published", StringComparison.OrdinalIgnoreCase))
                    ?? remainingPages.FirstOrDefault();
                version.DefaultPageId = replacement?.Id;
                if (replacement is null)
                {
                    version.Status = "Draft";
                }
                version.UpdatedBy = workspace.UserId;
                version.UpdatedTime = now;
                await db.Updateable(version).ExecuteCommandAsync(cancellationToken);
            }
        });
        return true;
    }

    public async Task<IReadOnlyList<ApplicationDevelopmentBusinessObjectDto>> GetBusinessObjectsAsync(
        string? versionId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var query = db.Queryable<ApplicationBusinessObjectDesignEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(versionId))
        {
            query = query.Where(item => item.VersionId == versionId);
        }

        var items = await query.OrderBy(item => item.CreatedTime, OrderByType.Asc).ToListAsync(cancellationToken);
        return items.Select(MapBusinessObject).ToArray();
    }

    public async Task<ApplicationDevelopmentBusinessObjectDto> GetBusinessObjectAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        return MapBusinessObject(await GetRequiredAsync<ApplicationBusinessObjectDesignEntity>(db, workspace, id, cancellationToken));
    }

    public async Task<ApplicationDevelopmentBusinessObjectDto> CreateBusinessObjectAsync(
        ApplicationDevelopmentBusinessObjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        ApplicationDevelopmentBusinessObjectDto? response = null;
        await ExecuteInTransactionAsync(db, async () =>
        {
            response = await CreateBusinessObjectCoreAsync(db, request, cancellationToken);
        });
        return response!;
    }

    private async Task<ApplicationDevelopmentBusinessObjectDto> CreateBusinessObjectCoreAsync(
        ISqlSugarClient db,
        ApplicationDevelopmentBusinessObjectUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var pageCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.PageCode, "业务对象页面编码");
        var modelCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.ModelCode, "业务对象模型编码");
        var modelName = ApplicationDataCenterCodePolicy.NormalizeName(
            string.IsNullOrWhiteSpace(request.ModelName) ? request.PageName : request.ModelName,
            "业务对象模型名称");
        var pageName = ApplicationDataCenterCodePolicy.NormalizeName(request.PageName, "业务对象页面名称");
        var menuCode = NormalizeBusinessObjectMenuCode(request.MenuCode, pageCode);
        await EnsureBusinessObjectScopeAsync(db, workspace, request.VersionId, request.ModuleId, cancellationToken);
        await EnsureUniquePageCodeAsync(db, workspace, pageCode, null, cancellationToken);
        await EnsureUniqueBusinessObjectCodeAsync(db, workspace, modelCode, null, cancellationToken);
        await EnsureUniqueMenuCodeAsync(db, workspace, menuCode, null, cancellationToken);
        var fields = NormalizeBusinessObjectFields(request.Fields);
        var businessObjectLayoutJson = NormalizeBusinessObjectLayout(request.DocumentJson, pageCode, pageName);
        var page = new ApplicationDevelopmentPageEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            VersionId = request.VersionId,
            ModuleId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.ModuleId),
            PageCode = pageCode,
            PageName = pageName,
            ParentPageId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.ParentPageId),
            PageParametersJson = "[]",
            PageType = ApplicationDevelopmentPageTypes.Standard,
            TemplateCode = "runtime-crud-page",
            DesignerMode = LatestDesignerMode,
            PermissionConfigJson = BuildBusinessObjectPermissionConfig(request.PermissionConfigJson, menuCode),
            SortOrder = request.SortOrder,
            Status = "Draft",
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        await db.Insertable(page).ExecuteCommandAsync(cancellationToken);
        await documentStore.SaveAsync(
            db,
            workspace,
            page.Id,
            page.VersionId,
            businessObjectLayoutJson,
            null,
            null,
            "{\"type\":\"business-object-created\"}",
            cancellationToken);
        await UpsertPreviewMenuAsync(db, workspace, page, cancellationToken);

        var design = new ApplicationBusinessObjectDesignEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            VersionId = request.VersionId,
            PageId = page.Id,
            PageCode = page.PageCode,
            PageName = page.PageName,
            ModuleId = page.ModuleId,
            ModelCode = modelCode,
            ModelName = modelName,
            MenuCode = menuCode,
            DataSourceId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.DataSourceId),
            SourceTable = ApplicationDataCenterCodePolicy.NormalizeOptional(request.SourceTable),
            ProviderKey = NormalizeProviderKey(request.ProviderKey),
            KeyField = ResolveBusinessObjectKeyField(request.KeyField, fields),
            FieldsJson = ApplicationDataCenterJson.Serialize(fields),
            PermissionConfigJson = page.PermissionConfigJson,
            CreateWorkflowBinding = request.CreateWorkflowBinding,
            Status = "Draft",
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false,
            Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000)
        };
        await db.Insertable(design).ExecuteCommandAsync(cancellationToken);
        return MapBusinessObject(design);
    }

    public async Task<ApplicationDevelopmentBusinessObjectDto> UpdateBusinessObjectAsync(
        string id,
        ApplicationDevelopmentBusinessObjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        ApplicationDevelopmentBusinessObjectDto? response = null;
        await ExecuteInTransactionAsync(db, async () =>
        {
            var workspace = workspaceResolver.Resolve();
            var design = await GetRequiredAsync<ApplicationBusinessObjectDesignEntity>(db, workspace, id, cancellationToken);
            var page = await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, design.PageId, cancellationToken);
            var pageCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.PageCode, "业务对象页面编码");
            var modelCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.ModelCode, "业务对象模型编码");
            var menuCode = NormalizeBusinessObjectMenuCode(request.MenuCode, pageCode);
            await EnsureBusinessObjectScopeAsync(db, workspace, request.VersionId, request.ModuleId, cancellationToken);
            await EnsureUniquePageCodeAsync(db, workspace, pageCode, page.Id, cancellationToken);
            await EnsureUniqueBusinessObjectCodeAsync(db, workspace, modelCode, design.Id, cancellationToken);
            await EnsureUniqueMenuCodeAsync(db, workspace, menuCode, page.PublishedMenuId, cancellationToken);
            var fields = NormalizeBusinessObjectFields(request.Fields);
            page.VersionId = request.VersionId;
            page.ModuleId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.ModuleId);
            page.PageCode = pageCode;
            page.PageName = ApplicationDataCenterCodePolicy.NormalizeName(request.PageName, "业务对象页面名称");
            page.ParentPageId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.ParentPageId);
            var businessObjectLayoutJson = NormalizeBusinessObjectLayout(request.DocumentJson, pageCode, page.PageName);
            var permissionConfigJson = BuildBusinessObjectPermissionConfig(request.PermissionConfigJson, menuCode);
            EnsurePermissionConfigurationAccess(page.PermissionConfigJson, permissionConfigJson);
            page.PermissionConfigJson = permissionConfigJson;
            page.TemplateCode = "runtime-crud-page";
            page.DesignerMode = LatestDesignerMode;
            page.SortOrder = request.SortOrder;
            page.Status = "Draft";
            page.PublishedArtifactId = null;
            page.PublishedMenuId = null;
            page.PublishedMenuCode = null;
            page.UpdatedBy = workspace.UserId;
            page.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(page).ExecuteCommandAsync(cancellationToken);
            var existingDocument = await documentStore.FindDocumentAsync(db, workspace, page.Id, cancellationToken);
            await documentStore.SaveAsync(
                db,
                workspace,
                page.Id,
                page.VersionId,
                businessObjectLayoutJson,
                existingDocument?.DocumentHash,
                null,
                "{\"type\":\"business-object-updated\"}",
                cancellationToken);
            await UpsertPreviewMenuAsync(db, workspace, page, cancellationToken);
            design.VersionId = request.VersionId;
            design.PageCode = page.PageCode;
            design.PageName = page.PageName;
            design.ModuleId = page.ModuleId;
            design.ModelCode = modelCode;
            design.ModelName = ApplicationDataCenterCodePolicy.NormalizeName(
                string.IsNullOrWhiteSpace(request.ModelName) ? request.PageName : request.ModelName,
                "业务对象模型名称");
            design.MenuCode = menuCode;
            design.DataSourceId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.DataSourceId);
            design.SourceTable = ApplicationDataCenterCodePolicy.NormalizeOptional(request.SourceTable);
            design.ProviderKey = NormalizeProviderKey(request.ProviderKey);
            design.KeyField = ResolveBusinessObjectKeyField(request.KeyField, fields);
            design.FieldsJson = ApplicationDataCenterJson.Serialize(fields);
            design.PermissionConfigJson = page.PermissionConfigJson;
            design.CreateWorkflowBinding = request.CreateWorkflowBinding;
            design.Status = "Draft";
            design.UpdatedBy = workspace.UserId;
            design.UpdatedTime = DateTime.UtcNow;
            design.Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000);
            await db.Updateable(design).ExecuteCommandAsync(cancellationToken);
            response = MapBusinessObject(design);
        });
        return response!;
    }

    public async Task<bool> DeleteBusinessObjectAsync(string id, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await ExecuteInTransactionAsync(db, async () =>
        {
            var design = await GetRequiredAsync<ApplicationBusinessObjectDesignEntity>(db, workspace, id, cancellationToken);
            var page = await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, design.PageId, cancellationToken);
            var now = DateTime.UtcNow;
            var publishedArtifactId = page.PublishedArtifactId;
            design.IsDeleted = true;
            design.DeletedBy = workspace.UserId;
            design.DeletedTime = now;
            design.UpdatedBy = workspace.UserId;
            design.UpdatedTime = now;
            page.IsDeleted = true;
            page.DeletedBy = workspace.UserId;
            page.DeletedTime = now;
            page.UpdatedBy = workspace.UserId;
            page.UpdatedTime = now;
            page.PublishedArtifactId = null;
            page.PublishedMenuId = null;
            page.PublishedMenuCode = null;
            await db.Updateable(design).ExecuteCommandAsync(cancellationToken);
            await db.Updateable(page).ExecuteCommandAsync(cancellationToken);
            await InvalidateDeletedPageRuntimeAsync(db, workspace, page, publishedArtifactId, now, cancellationToken);
        });
        return true;
    }

    private static async Task InvalidateDeletedPageRuntimeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity page,
        string? publishedArtifactId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await db.Updateable<SystemMenuEntity>()
            .SetColumns(item => new SystemMenuEntity
            {
                IsDeleted = true,
                DeletedBy = workspace.UserId,
                DeletedTime = now,
                UpdatedBy = workspace.UserId,
                UpdatedTime = now,
                Visible = false
            })
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                           !item.IsDeleted &&
                           (item.PageCode == page.PageCode ||
                            (!string.IsNullOrWhiteSpace(publishedArtifactId) && item.ArtifactId == publishedArtifactId)))
            .ExecuteCommandAsync(cancellationToken);

        await db.Updateable<ApplicationDesignerDocumentEntity>()
            .SetColumns(item => new ApplicationDesignerDocumentEntity
            {
                IsDeleted = true,
                DeletedBy = workspace.UserId,
                DeletedTime = now,
                UpdatedBy = workspace.UserId,
                UpdatedTime = now,
                Status = "Deleted",
                PublishedArtifactId = null
            })
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                           item.PageId == page.Id && !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);

        var documentIds = await db.Queryable<ApplicationDesignerDocumentEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.PageId == page.Id)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (documentIds.Count > 0)
        {
            await db.Updateable<ApplicationDesignerRuntimeArtifactEntity>()
                .SetColumns(item => new ApplicationDesignerRuntimeArtifactEntity
                {
                    IsDeleted = true,
                    DeletedBy = workspace.UserId,
                    DeletedTime = now,
                    UpdatedBy = workspace.UserId,
                    UpdatedTime = now,
                    Status = "Deleted"
                })
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                               documentIds.Contains(item.DocumentId) && !item.IsDeleted)
                .ExecuteCommandAsync(cancellationToken);
        }

        await db.Updateable<ApplicationDesignerPublishRecordEntity>()
            .SetColumns(item => new ApplicationDesignerPublishRecordEntity
            {
                IsDeleted = true,
                DeletedBy = workspace.UserId,
                DeletedTime = now,
                UpdatedBy = workspace.UserId,
                UpdatedTime = now,
                Status = "Deleted"
            })
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                           item.PageId == page.Id && !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApplicationDevelopmentPreviewSchemaResponse> PreviewBusinessObjectAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var design = await GetRequiredAsync<ApplicationBusinessObjectDesignEntity>(db, workspace, id, cancellationToken);
        var page = await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, design.PageId, cancellationToken);
        var fields = ReadBusinessObjectFields(design.FieldsJson);
        var document = await documentStore.RequireCurrentAsync(db, workspace, page.Id, cancellationToken);
        var schema = CompileBusinessObjectSchema(page, design, fields, document.DocumentJson, ReadDocumentPageMetadata(document.DocumentJson).PageType);
        return new ApplicationDevelopmentPreviewSchemaResponse { PageCode = page.PageCode, PageName = page.PageName, ArtifactJson = schema };
    }

    public async Task<ApplicationDevelopmentBusinessObjectPublishResponse> PublishBusinessObjectAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        ApplicationDevelopmentBusinessObjectPublishResponse? response = null;
        await ExecuteInTransactionAsync(db, async () =>
        {
            response = await PublishBusinessObjectCoreAsync(db, id, cancellationToken);
        });
        return response!;
    }

    private async Task<ApplicationDevelopmentBusinessObjectPublishResponse> PublishBusinessObjectCoreAsync(
        ISqlSugarClient db,
        string id,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var design = await GetRequiredAsync<ApplicationBusinessObjectDesignEntity>(db, workspace, id, cancellationToken);
        var page = await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, design.PageId, cancellationToken);
        var document = await documentStore.RequireCurrentAsync(db, workspace, page.Id, cancellationToken);
        var fields = ReadBusinessObjectFields(design.FieldsJson);
        var keyField = ResolveBusinessObjectKeyField(design.KeyField, fields);
        var readOnly = string.IsNullOrWhiteSpace(keyField);
        var warnings = readOnly ? new[] { "未发现主键字段，已生成只读运行页面，写入与导入能力已禁用。" } : Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(design.DataSourceId) && string.IsNullOrWhiteSpace(design.SourceTable))
        {
            throw new ValidationException("业务对象必须配置数据源或源表，不能生成无数据来源的运行模型", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var permissionConfig = ApplicationDataCenterJson.Deserialize<ApplicationDevelopmentPermissionConfigDto>(design.PermissionConfigJson)
            ?? new ApplicationDevelopmentPermissionConfigDto();
        var permissionCodes = BuildBusinessObjectPermissionCodes(page.PageCode, permissionConfig, readOnly);
        await UpsertPermissionCodesAsync(db, permissionCodes, page.PageName, workspace.UserId, cancellationToken);
        await GrantRuntimePermissionsAsync(db, workspace, permissionCodes, permissionConfig.RoleCodes, cancellationToken);
        var runtimeFields = fields.Select(MapRuntimeField).ToArray();
        var modelSchema = ApplicationDataCenterJson.Serialize(new
        {
            idGeneration = "guid",
            source = new { dataSourceId = design.DataSourceId, tableName = design.SourceTable },
            fields = runtimeFields,
            operations = Array.Empty<object>()
        });
        var model = await UpsertBusinessObjectDataModelAsync(db, workspace, design, page.PageCode, runtimeFields, keyField, modelSchema, readOnly, cancellationToken);
        var documentPageType = ReadDocumentPageMetadata(document.DocumentJson).PageType;
        var schemaJson = CompileBusinessObjectSchema(page, design, fields, document.DocumentJson, documentPageType, runtimeFields, keyField, readOnly, permissionConfig);
        EnsurePublishedArtifactDocumentParity(schemaJson, document);
        var publishedArtifact = await artifactPublisher.PublishAsync(
            db,
            workspace,
            document,
            schemaJson,
            document.PublishedArtifactId,
            cancellationToken);
        page.PublishedArtifactId = publishedArtifact.Id;
        var menu = await UpsertPublishedMenuAsync(db, workspace, page, permissionConfig, PermissionCodes.BuildAppRuntimePagePermission(page.PageCode, "view"), cancellationToken);
        page.PublishedMenuId = menu.Id;
        page.PublishedMenuCode = menu.MenuCode;
        page.Status = "Published";
        page.UpdatedBy = workspace.UserId;
        page.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(page).ExecuteCommandAsync(cancellationToken);
        design.Status = "Published";
        design.KeyField = keyField;
        design.UpdatedBy = workspace.UserId;
        design.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(design).ExecuteCommandAsync(cancellationToken);
        var nextActions = design.CreateWorkflowBinding
            ? [new ApplicationDevelopmentNextActionDto
            {
                Code = "configure-workflow-binding",
                Route = "/workflows/bindings",
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["formResourceCode"] = page.PageCode,
                    ["pageCode"] = page.PageCode,
                    ["menuCode"] = menu.MenuCode
                }
            }]
            : Array.Empty<ApplicationDevelopmentNextActionDto>();
        return new ApplicationDevelopmentBusinessObjectPublishResponse
        {
            BusinessObjectId = design.Id,
            PageCode = page.PageCode,
            ModelCode = design.ModelCode,
            MenuCode = menu.MenuCode,
            ArtifactId = publishedArtifact.Id,
            DataModelId = model.Id,
            GeneratedPermissionCodes = permissionCodes,
            Warnings = warnings,
            NextActions = nextActions
        };
    }

    public async Task<IReadOnlyList<ApplicationDevelopmentSharedResourceListItemDto>> GetSharedResourcesAsync(
        string? versionId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(versionId))
        {
            await GetRequiredAsync<ApplicationDevelopmentVersionEntity>(db, workspace, versionId, cancellationToken);
        }

        var query = db.Queryable<ApplicationSharedResourceEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(versionId))
        {
            query = query.Where(item => item.VersionId == versionId);
        }

        var items = await query
            .OrderBy(item => item.ResourceType, OrderByType.Asc)
            .OrderBy(item => item.ResourceCode, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return items.Select(MapSharedResourceListItem).ToArray();
    }

    public async Task<ApplicationDevelopmentSharedResourceDetailDto> GetSharedResourceAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await GetRequiredAsync<ApplicationSharedResourceEntity>(db, workspace, id, cancellationToken);
        return MapSharedResourceDetail(entity);
    }

    public async Task<ApplicationDevelopmentSharedResourceDetailDto> CreateSharedResourceAsync(
        ApplicationDevelopmentSharedResourceUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var resourceCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.ResourceCode, "资源编码");
        await EnsureSharedResourceScopeAsync(db, workspace, request.VersionId, cancellationToken);
        await EnsureUniqueSharedResourceCodeAsync(db, workspace, resourceCode, null, cancellationToken);
        var entity = new ApplicationSharedResourceEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            VersionId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.VersionId),
            ResourceCode = resourceCode,
            ResourceName = ApplicationDataCenterCodePolicy.NormalizeName(request.ResourceName, "资源名称"),
            ResourceType = NormalizeResourceType(request.ResourceType),
            Status = NormalizeResourceStatus(request.Status),
            ContentJson = NormalizeSharedResourceJson(request.ContentJson),
            ContentText = ApplicationDataCenterCodePolicy.NormalizeOptional(request.ContentText, 200000),
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapSharedResourceDetail(entity);
    }

    public async Task<ApplicationDevelopmentSharedResourceDetailDto> UpdateSharedResourceAsync(
        string id,
        ApplicationDevelopmentSharedResourceUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await GetRequiredAsync<ApplicationSharedResourceEntity>(db, workspace, id, cancellationToken);
        var resourceCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.ResourceCode, "资源编码");
        await EnsureSharedResourceScopeAsync(db, workspace, request.VersionId, cancellationToken);
        await EnsureUniqueSharedResourceCodeAsync(db, workspace, resourceCode, id, cancellationToken);
        entity.VersionId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.VersionId);
        entity.ResourceCode = resourceCode;
        entity.ResourceName = ApplicationDataCenterCodePolicy.NormalizeName(request.ResourceName, "资源名称");
        entity.ResourceType = NormalizeResourceType(request.ResourceType);
        entity.Status = NormalizeResourceStatus(request.Status);
        entity.ContentJson = NormalizeSharedResourceJson(request.ContentJson);
        entity.ContentText = ApplicationDataCenterCodePolicy.NormalizeOptional(request.ContentText, 200000);
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return MapSharedResourceDetail(entity);
    }

    public async Task<ApplicationDevelopmentPermissionOptionsResponse> GetPermissionOptionsAsync(CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var roles = await db.Queryable<SystemRoleEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.IsEnabled && !item.IsDeleted)
            .OrderBy(item => item.RoleName, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var menus = await db.Queryable<SystemMenuEntity>()
            .Where(item =>
                item.TenantId == workspace.TenantId &&
                item.AppCode == workspace.AppCode &&
                item.Visible &&
                !item.IsDeleted &&
                item.MenuType != "Button" &&
                item.MenuType != "按钮")
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return new ApplicationDevelopmentPermissionOptionsResponse
        {
            MenuOptions = menus.Select(item => new ApplicationDevelopmentMenuOptionDto { MenuCode = item.MenuCode, MenuName = item.MenuName }).ToList(),
            RoleOptions = roles.Select(item => new ApplicationDevelopmentRoleOptionDto { RoleCode = item.RoleCode, RoleId = item.Id, RoleName = item.RoleName }).ToList()
        };
    }

    public async Task<ApplicationDevelopmentPreviewSchemaResponse> GetPreviewSchemaAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var page = await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, pageId, cancellationToken);
        var document = await documentStore.RequireCurrentAsync(db, workspace, page.Id, cancellationToken);
        ValidateNoDeprecatedModelDirectBinding(document.DocumentJson);
        await ValidateDesignerOperationTargetsAsync(db, workspace, page, document.DocumentJson, cancellationToken);
        await runtimeEnvironmentCheckService.EnsurePassedAsync(document.DocumentJson, cancellationToken);
        var documentMetadata = ReadDocumentPageMetadata(document.DocumentJson);
        var pageParameters = documentMetadata.Parameters;
        var schemaJson = schemaCompiler.CompileSchema(
            page.PageCode,
            page.PageName,
            documentMetadata.PageType,
            pageParameters,
            document.DocumentJson,
            page.PermissionConfigJson);
        return new ApplicationDevelopmentPreviewSchemaResponse
        {
            PageCode = page.PageCode,
            PageName = page.PageName,
            ArtifactJson = schemaJson
        };
    }

    public async Task<ApplicationDevelopmentPreviewSchemaResponse> CompilePreviewArtifactAsync(
        string pageId,
        ApplicationDevelopmentPreviewArtifactRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentJson))
        {
            throw new ValidationException("Designer Document cannot be empty", ErrorCodes.DesignerSchemaInvalid);
        }

        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var page = await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, pageId, cancellationToken);
        ValidateNoDeprecatedModelDirectBinding(request.DocumentJson);
        await ValidateDesignerOperationTargetsAsync(db, workspace, page, request.DocumentJson, cancellationToken);
        await runtimeEnvironmentCheckService.EnsurePassedAsync(request.DocumentJson, cancellationToken);

        var documentMetadata = ReadDocumentPageMetadata(request.DocumentJson);
        var schemaJson = schemaCompiler.CompileSchema(
            page.PageCode,
            page.PageName,
            documentMetadata.PageType,
            documentMetadata.Parameters,
            request.DocumentJson,
            page.PermissionConfigJson);

        return new ApplicationDevelopmentPreviewSchemaResponse
        {
            PageCode = page.PageCode,
            PageName = page.PageName,
            ArtifactJson = schemaJson
        };
    }

    public async Task<ApplicationDevelopmentPageDetailDto> RefreshPreviewMenuAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var page = await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, pageId, cancellationToken);
        await UpsertPreviewMenuAsync(db, workspace, page, cancellationToken);
        return await MapPageDetailAsync(db, workspace, page, cancellationToken);
    }

    public async Task<ApplicationDevelopmentEnvironmentCheckResponse> CheckPageEnvironmentAsync(
        string pageId,
        ApplicationDevelopmentEnvironmentCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, pageId, cancellationToken);
        return await runtimeEnvironmentCheckService.CheckAsync(request.DocumentJson, cancellationToken);
    }

    public async Task<ApplicationDevelopmentPublishResponse> PublishPageAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        ApplicationDevelopmentPublishResponse? response = null;
        await ExecuteInTransactionAsync(db, async () =>
        {
            response = await PublishPageCoreAsync(db, pageId, cancellationToken);
        });
        return response!;
    }

    private async Task<ApplicationDevelopmentPublishResponse> PublishPageCoreAsync(
        ISqlSugarClient db,
        string pageId,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var page = await GetRequiredAsync<ApplicationDevelopmentPageEntity>(db, workspace, pageId, cancellationToken);
        var document = await documentStore.RequireCurrentAsync(db, workspace, page.Id, cancellationToken);
        var permissionConfig = ApplicationDataCenterJson.Deserialize<ApplicationDevelopmentPermissionConfigDto>(page.PermissionConfigJson)
            ?? new ApplicationDevelopmentPermissionConfigDto();
        ValidateNoDeprecatedModelDirectBinding(document.DocumentJson);
        await ValidateDesignerOperationTargetsAsync(db, workspace, page, document.DocumentJson, cancellationToken);
        await runtimeEnvironmentCheckService.EnsurePassedAsync(document.DocumentJson, cancellationToken);
        var viewPermission = PermissionCodes.BuildAppRuntimePagePermission(page.PageCode, "view");
        var documentMetadata = ReadDocumentPageMetadata(document.DocumentJson);
        var pageType = documentMetadata.PageType;
        var pageParameters = documentMetadata.Parameters;
        var publishedArtifactJson = schemaCompiler.CompileSchema(
            page.PageCode,
            page.PageName,
            pageType,
            pageParameters,
            document.DocumentJson,
            page.PermissionConfigJson);
        EnsurePublishedArtifactDocumentParity(publishedArtifactJson, document);
        var publishedArtifact = await artifactPublisher.PublishAsync(db, workspace, document, publishedArtifactJson, document.PublishedArtifactId, cancellationToken);
        var permissionCodes = BuildPermissionCodes(page.PageCode, permissionConfig);
        await UpsertPermissionCodesAsync(db, permissionCodes, page.PageName, workspace.UserId, cancellationToken);
        await GrantRuntimePermissionsAsync(db, workspace, permissionCodes, permissionConfig.RoleCodes, cancellationToken);
        page.PublishedArtifactId = publishedArtifact.Id;
        var publishedMenu = await UpsertPublishedMenuAsync(db, workspace, page, permissionConfig, viewPermission, cancellationToken);
        page.PublishedMenuCode = publishedMenu.MenuCode;
        page.PublishedMenuId = publishedMenu.Id;
        page.Status = "Published";
        page.UpdatedBy = workspace.UserId;
        page.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(page).ExecuteCommandAsync(cancellationToken);

        var version = await GetRequiredAsync<ApplicationDevelopmentVersionEntity>(db, workspace, page.VersionId, cancellationToken);
        version.Status = "Published";
        version.DefaultPageId ??= page.Id;
        version.UpdatedBy = workspace.UserId;
        version.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(version).ExecuteCommandAsync(cancellationToken);
        return new ApplicationDevelopmentPublishResponse
        {
            GeneratedPermissionCodes = permissionCodes,
            Diagnostics = [],
            PublishedMenuCode = publishedMenu.MenuCode,
            PublishedMenuId = publishedMenu.Id,
            PublishedArtifactId = publishedArtifact.Id,
            PublishedArtifactHash = publishedArtifact.ArtifactHash,
            PublishedArtifactRevision = publishedArtifact.RevisionNumber,
            PublishedManifestHash = publishedArtifact.ManifestHash,
            PublishedRoutePath = BuildPublishedRoute(page.PageCode),
            PublishedSchemaUpdatedTime = publishedArtifact.PublishedTime,
            VersionId = page.VersionId
        };
    }

    private static void EnsurePublishedArtifactDocumentParity(
        string publishedArtifactJson,
        ApplicationDesignerDocumentEntity document)
    {
        var schema = JsonNode.Parse(publishedArtifactJson) as JsonObject
            ?? throw new ValidationException("发布产物必须是 JSON 对象", ErrorCodes.DesignerSchemaInvalid);
        var artifactDocument = schema["document"]
            ?? throw new ValidationException("发布产物缺少 DesignerDocument", ErrorCodes.DesignerSchemaInvalid);
        var canonicalArtifactDocument = NormalizePublishedDocumentForParity(
            artifactDocument.ToJsonString(ApplicationDataCenterJson.Options));
        var artifactDocumentHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(canonicalArtifactDocument);
        var persistedDocumentHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(
            NormalizePublishedDocumentForParity(document.DocumentJson));
        if (!string.Equals(artifactDocumentHash, persistedDocumentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("发布产物与持久化 DesignerDocument 不一致", ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private static string NormalizePublishedDocumentForParity(string documentJson)
    {
        var document = JsonNode.Parse(ApplicationDesignerCanonicalJson.NormalizeObject(documentJson)) as JsonObject
            ?? throw new ValidationException("Designer Document must be a JSON object", ErrorCodes.DesignerSchemaInvalid);

        // These values are derived from page metadata and permission configuration during compilation.
        // They belong to the runtime projection, not to the persisted editor source contract.
        document.Remove("pageType");
        document.Remove("pageParameters");
        if (document["runtimeContext"] is JsonObject runtimeContext)
        {
            runtimeContext.Remove("pageType");
            runtimeContext.Remove("pageParameters");
            runtimeContext.Remove("permissionPrefix");
            runtimeContext.Remove("menuCode");
            runtimeContext.Remove("modelCode");
        }

        return document.ToJsonString(ApplicationDataCenterJson.Options);
    }

    private static async Task ExecuteInTransactionAsync(ISqlSugarClient db, Func<Task> action)
    {
        var ownsTransaction = db.Ado.Transaction is null;
        if (ownsTransaction)
        {
            await db.Ado.BeginTranAsync();
        }

        try
        {
            await action();
            if (ownsTransaction)
            {
                await db.Ado.CommitTranAsync();
            }
        }
        catch
        {
            if (ownsTransaction)
            {
                await db.Ado.RollbackTranAsync();
            }

            throw;
        }
    }

    public async Task<ApplicationDevelopmentPublishResponse> PublishVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        ApplicationDevelopmentPublishResponse? response = null;
        await ExecuteInTransactionAsync(db, async () =>
        {
            response = await PublishVersionCoreAsync(db, versionId, cancellationToken);
        });
        return response!;
    }

    private async Task<ApplicationDevelopmentPublishResponse> PublishVersionCoreAsync(
        ISqlSugarClient db,
        string versionId,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        await GetRequiredAsync<ApplicationDevelopmentVersionEntity>(db, workspace, versionId, cancellationToken);
        var pages = await db.Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.VersionId == versionId && !item.IsDeleted)
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        if (pages.Count == 0)
        {
            throw new ValidationException("当前版本没有可发布页面", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var generatedPermissionCodes = new List<string>();
        ApplicationDevelopmentPublishResponse? last = null;
        foreach (var page in pages)
        {
            last = await PublishPageCoreAsync(db, page.Id, cancellationToken);
            generatedPermissionCodes.AddRange(last.GeneratedPermissionCodes);
        }

        return new ApplicationDevelopmentPublishResponse
        {
            GeneratedPermissionCodes = generatedPermissionCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Diagnostics = [],
            PublishedMenuCode = last?.PublishedMenuCode,
            PublishedMenuId = last?.PublishedMenuId,
            PublishedArtifactId = last?.PublishedArtifactId,
            PublishedArtifactHash = last?.PublishedArtifactHash,
            PublishedArtifactRevision = last?.PublishedArtifactRevision,
            PublishedManifestHash = last?.PublishedManifestHash,
            PublishedRoutePath = last?.PublishedRoutePath,
            PublishedSchemaUpdatedTime = last?.PublishedSchemaUpdatedTime,
            VersionId = versionId
        };
    }

    private async Task<SystemTenantAppEntity> GetTenantAppAsync(CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var tenantApp = (await mainDb.Queryable<SystemTenantAppEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        return tenantApp ?? throw new ValidationException("当前应用工作区不存在或已停用", ErrorCodes.PermissionDenied);
    }

    private async Task<TEntity> GetRequiredAsync<TEntity>(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string id,
        CancellationToken cancellationToken)
        where TEntity : class, new()
    {
        var entity = (await db.Queryable<TEntity>().Where($"Id=@id", new { id }).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        return entity switch
        {
            ApplicationDevelopmentVersionEntity version when version.TenantId == workspace.TenantId && version.AppCode == workspace.AppCode && !version.IsDeleted => entity,
            ApplicationDevelopmentModuleEntity module when module.TenantId == workspace.TenantId && module.AppCode == workspace.AppCode && !module.IsDeleted => entity,
            ApplicationDevelopmentPageEntity page when page.TenantId == workspace.TenantId && page.AppCode == workspace.AppCode && !page.IsDeleted => entity,
            ApplicationSharedResourceEntity resource when resource.TenantId == workspace.TenantId && resource.AppCode == workspace.AppCode && !resource.IsDeleted => entity,
            _ => throw new NotFoundException("开发中心对象不存在", ErrorCodes.ApplicationDataCenterObjectNotFound)
        };
    }

    private static ApplicationDevelopmentVersionDto MapVersion(ApplicationDevelopmentVersionEntity entity) =>
        new()
        {
            Id = entity.Id,
            DefaultPageId = entity.DefaultPageId,
            SourceDataSourceId = entity.SourceDataSourceId,
            Status = entity.Status,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime,
            VersionCode = entity.VersionCode,
            VersionName = entity.VersionName
        };

    private static ApplicationDevelopmentPageListItemDto MapPageListItem(
        ApplicationDevelopmentPageEntity entity,
        ApplicationDesignerRuntimeArtifactEntity? publishedArtifact) =>
        new()
        {
            Id = entity.Id,
            ModuleId = entity.ModuleId,
            PageCode = entity.PageCode,
            PageName = entity.PageName,
            ParentPageId = entity.ParentPageId,
            PageParameters = ReadPageParameters(entity.PageParametersJson),
            PageParametersJson = entity.PageParametersJson,
            PageType = NormalizePageType(entity.PageType),
            PreviewMenuCode = entity.PreviewMenuCode,
            PreviewRoutePath = publishedArtifact is null ? null : BuildPreviewRoute(entity),
            PublishedMenuCode = entity.PublishedMenuCode,
            PublishedArtifactId = publishedArtifact?.Id,
            PublishedRoutePath = publishedArtifact is null ? null : BuildPublishedRoute(entity.PageCode),
            SortOrder = entity.SortOrder,
            Status = entity.Status,
            TemplateCode = entity.TemplateCode,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime,
            VersionId = entity.VersionId
        };

    private async Task<ApplicationDevelopmentPageDetailDto> MapPageDetailAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity entity,
        CancellationToken cancellationToken)
    {
        var pageParameters = ReadPageParameters(entity.PageParametersJson);
        var pageType = NormalizePageType(entity.PageType);
        var latestDocument = await documentStore.RequireCurrentAsync(db, workspace, entity.Id, cancellationToken);
        var publishedArtifact = await TryLoadPublishedArtifactAsync(db, workspace, latestDocument, cancellationToken);
        if (!string.IsNullOrWhiteSpace(latestDocument.PublishedArtifactId) && publishedArtifact is null)
        {
            throw new ValidationException(
                "The published DesignerDocument points to a missing Runtime Artifact",
                ErrorCodes.DesignerSchemaInvalid);
        }
        var dto = new ApplicationDevelopmentPageDetailDto
        {
            DesignerMode = entity.DesignerMode,
            Id = entity.Id,
            UpdatedTime = entity.UpdatedTime,
            DocumentJson = latestDocument.DocumentJson,
            ModuleId = entity.ModuleId,
            PageCode = entity.PageCode,
            PageName = entity.PageName,
            ParentPageId = entity.ParentPageId,
            PageParameters = pageParameters,
            PageParametersJson = ApplicationDataCenterJson.Serialize(pageParameters),
            PageType = pageType,
            PermissionConfigJson = entity.PermissionConfigJson,
            PreviewMenuCode = entity.PreviewMenuCode,
            PreviewRoutePath = publishedArtifact is null ? null : BuildPreviewRoute(entity),
            PublishedMenuCode = entity.PublishedMenuCode,
            PublishedMenuId = entity.PublishedMenuId,
            PublishedArtifactId = publishedArtifact?.Id,
            PublishedArtifactHash = publishedArtifact?.ArtifactHash,
            PublishedArtifactRevision = publishedArtifact?.RevisionNumber,
            PublishedManifestHash = publishedArtifact?.ManifestHash,
            PublishedRoutePath = publishedArtifact is null ? null : BuildPublishedRoute(entity.PageCode),
            PublishedArtifactJson = publishedArtifact?.ArtifactJson,
            PublishedSchemaUpdatedTime = publishedArtifact?.PublishedTime,
            SortOrder = entity.SortOrder,
            Status = entity.Status,
            TemplateCode = entity.TemplateCode,
            VersionId = entity.VersionId
        };

        return dto;
    }

    private static async Task<IReadOnlyDictionary<string, ApplicationDesignerRuntimeArtifactEntity>> LoadPublishedPageArtifactsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        IReadOnlyList<ApplicationDevelopmentPageEntity> pages,
        CancellationToken cancellationToken)
    {
        if (pages.Count == 0)
        {
            return new Dictionary<string, ApplicationDesignerRuntimeArtifactEntity>();
        }

        var pageIds = pages.Select(item => item.Id).ToArray();
        var documents = await db.Queryable<ApplicationDesignerDocumentEntity>()
            .Where(item =>
                item.TenantId == workspace.TenantId &&
                item.AppCode == workspace.AppCode &&
                pageIds.Contains(item.PageId) &&
                item.Status == "Published" &&
                !item.IsDeleted &&
                item.PublishedArtifactId != null && item.PublishedArtifactId != "")
            .ToListAsync(cancellationToken);

        var artifactIds = documents.Select(item => item.PublishedArtifactId!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var artifacts = await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                           artifactIds.Contains(item.Id) && item.Status == "Published" && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var artifactsById = artifacts.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        return documents
            .Select(document => artifactsById.TryGetValue(document.PublishedArtifactId!, out var artifact)
                ? (document.PageId, Artifact: artifact)
                : (document.PageId, Artifact: (ApplicationDesignerRuntimeArtifactEntity?)null))
            .Where(item => item.Artifact is not null)
            .ToDictionary(item => item.PageId, item => item.Artifact!, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<ApplicationDesignerRuntimeArtifactEntity?> TryLoadPublishedArtifactAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDesignerDocumentEntity document,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.PublishedArtifactId))
        {
            return null;
        }

        return (await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                           item.Id == document.PublishedArtifactId && item.DocumentId == document.Id &&
                           item.Status == "Published" && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
    }

    private static ApplicationDevelopmentSharedResourceListItemDto MapSharedResourceListItem(ApplicationSharedResourceEntity entity) =>
        new()
        {
            Id = entity.Id,
            ResourceCode = entity.ResourceCode,
            ResourceName = entity.ResourceName,
            ResourceType = entity.ResourceType,
            Status = entity.Status,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime,
            VersionId = entity.VersionId
        };

    private static ApplicationDevelopmentSharedResourceDetailDto MapSharedResourceDetail(ApplicationSharedResourceEntity entity) =>
        new()
        {
            ContentJson = entity.ContentJson,
            ContentText = entity.ContentText,
            Id = entity.Id,
            ResourceCode = entity.ResourceCode,
            ResourceName = entity.ResourceName,
            ResourceType = entity.ResourceType,
            Status = entity.Status,
            VersionId = entity.VersionId
        };

    private async Task EnsureUniqueVersionCodeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string versionCode,
        string? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<ApplicationDevelopmentVersionEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.VersionCode == versionCode && !item.IsDeleted && item.Id != excludeId)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException($"版本编码已存在: {versionCode}", ErrorCodes.ApplicationDataCenterDuplicateCode);
        }
    }

    private async Task EnsureUniqueModuleCodeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string versionId,
        string moduleCode,
        string? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<ApplicationDevelopmentModuleEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.VersionId == versionId && item.ModuleCode == moduleCode && !item.IsDeleted && item.Id != excludeId)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException($"模块编码已存在: {moduleCode}", ErrorCodes.ApplicationDataCenterDuplicateCode);
        }
    }

    private async Task<string> GenerateModuleCodeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string versionId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = $"menu_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Random.Shared.Next(1000, 9999)}";
            var exists = await db.Queryable<ApplicationDevelopmentModuleEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.VersionId == versionId && item.ModuleCode == code && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        throw new ValidationException("菜单编码自动生成失败，请重试", ErrorCodes.ApplicationDataCenterDuplicateCode);
    }

    private async Task EnsureUniquePageCodeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string pageCode,
        string? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.PageCode == pageCode && !item.IsDeleted && item.Id != excludeId)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException($"页面编码已存在: {pageCode}", ErrorCodes.ApplicationDataCenterDuplicateCode);
        }
    }

    private async Task EnsureUniqueSharedResourceCodeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string resourceCode,
        string? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<ApplicationSharedResourceEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.ResourceCode == resourceCode && !item.IsDeleted && item.Id != excludeId)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException($"共享资源编码已存在: {resourceCode}", ErrorCodes.ApplicationDataCenterDuplicateCode);
        }
    }

    private async Task EnsureSharedResourceScopeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string? versionId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(versionId))
        {
            await GetRequiredAsync<ApplicationDevelopmentVersionEntity>(db, workspace, versionId, cancellationToken);
        }
    }

    private async Task ValidateDesignerOperationTargetsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity page,
        string canonicalDocumentJson,
        CancellationToken cancellationToken)
    {
        var diagnostics = await BuildDesignerPublishDiagnosticsAsync(db, workspace, page, canonicalDocumentJson, cancellationToken);
        var firstError = diagnostics.FirstOrDefault(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase));
        if (firstError is not null)
        {
            throw new ValidationException($"{firstError.Message}（{firstError.Code}）", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private async Task<IReadOnlyList<ApplicationDevelopmentPublishDiagnosticDto>> BuildDesignerPublishDiagnosticsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity page,
        string canonicalDocumentJson,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ApplicationDevelopmentPublishDiagnosticDto>();
        var targetRequirements = new List<DesignerOperationTargetRequirement>();
        using var document = JsonDocument.Parse(canonicalDocumentJson);
        var modalIds = ReadDesignerModalIds(document.RootElement);
        if (!document.RootElement.TryGetProperty("elements", out var elements) ||
            elements.ValueKind != JsonValueKind.Object)
        {
            return diagnostics;
        }

        foreach (var element in elements.EnumerateObject())
        {
            if (element.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            CollectDesignerActionStepRequirements(element.Name, element.Value, modalIds, diagnostics, targetRequirements);
            if (!string.Equals(ReadJsonString(element.Value, "type"), "report.dataTable", StringComparison.Ordinal) ||
                !element.Value.TryGetProperty("props", out var props) ||
                props.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            CollectDesignerTableDiagnostics(element.Name, element.Value, props, diagnostics, targetRequirements);
        }

        await ValidateDesignerTargetPagesAsync(db, workspace, page, targetRequirements, diagnostics, cancellationToken);
        return diagnostics;
    }

    private static void CollectDesignerTableDiagnostics(
        string elementId,
        JsonElement element,
        JsonElement props,
        List<ApplicationDevelopmentPublishDiagnosticDto> diagnostics,
        List<DesignerOperationTargetRequirement> targetRequirements)
    {
        var tableName = ReadJsonString(element, "name") ?? elementId;
        if (props.TryGetProperty("rowEditing", out var rowEditing) &&
            rowEditing.ValueKind == JsonValueKind.Object &&
            ReadJsonString(rowEditing, "mode")?.Trim().ToLowerInvariant() is ApplicationDevelopmentPageTypes.Dialog or ApplicationDevelopmentPageTypes.Drawer)
        {
            diagnostics.Add(PublishDiagnostic(
                "designer.table.legacy_row_editing_shell",
                $"表格 {tableName} 仍使用旧 rowEditing 弹框/抽屉业务编辑壳，请迁移为操作列页面调用。",
                elementId,
                null,
                $"elements.{elementId}.props.rowEditing",
                "在交互区新增行操作，展示方式选择弹框页面或抽屉页面，并绑定正式目标页面。"));
        }

        if (props.TryGetProperty("columns", out var columns) && columns.ValueKind == JsonValueKind.Array)
        {
            var columnIndex = 0;
            foreach (var column in columns.EnumerateArray())
            {
                if (column.ValueKind == JsonValueKind.Object &&
                    string.IsNullOrWhiteSpace(ReadJsonString(column, "fieldCode") ?? ReadJsonString(column, "key")))
                {
                    diagnostics.Add(PublishDiagnostic(
                        "designer.table.column_missing_field_code",
                        $"表格 {tableName} 第 {columnIndex + 1} 列缺少稳定 fieldCode。",
                        elementId,
                        null,
                        $"elements.{elementId}.props.columns[{columnIndex}]",
                        "从微流输出契约同步列，或在列配置中补齐字段编码。"));
                }

                columnIndex++;
            }
        }

        if (!props.TryGetProperty("rowActions", out var rowActions) || rowActions.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var actionIndex = 0;
        foreach (var rowAction in rowActions.EnumerateArray())
        {
            if (rowAction.ValueKind != JsonValueKind.Object)
            {
                actionIndex++;
                continue;
            }

            var actionId = ReadJsonString(rowAction, "id") ?? $"rowAction[{actionIndex}]";
            var presentation = ReadJsonString(rowAction, "presentation")?.Trim().ToLowerInvariant();
            var label = ReadJsonString(rowAction, "label") ?? "未命名操作";
            var path = $"elements.{elementId}.props.rowActions[{actionIndex}]";
            if (presentation is ApplicationDevelopmentPageTypes.Dialog or ApplicationDevelopmentPageTypes.Drawer)
            {
                var targetPageCode = ReadJsonString(rowAction, "targetPageCode")
                    ?? ReadJsonString(rowAction, "target")
                    ?? ReadInvocationString(rowAction, "targetPageCode")
                    ?? ReadInvocationString(rowAction, "target");
                var targetPageId = ReadJsonString(rowAction, "targetPageId") ?? ReadInvocationString(rowAction, "targetPageId");
                if (string.IsNullOrWhiteSpace(targetPageCode) && string.IsNullOrWhiteSpace(targetPageId))
                {
                    diagnostics.Add(PublishDiagnostic(
                        "designer.operation.target_page_missing",
                        $"表格 {tableName} 的操作 {label} 使用 {presentation} 展示方式时必须绑定目标页面。",
                        elementId,
                        actionId,
                        path,
                        "在交互区的操作列配置中选择目标弹框页或抽屉页。"));
                }
                else
                {
                    targetRequirements.Add(new DesignerOperationTargetRequirement(
                        actionId,
                        elementId,
                        ReadInvocationString(rowAction, "targetPageType") ?? ReadJsonString(rowAction, "targetPageType"),
                        ReadInvocationMappingTargets(rowAction),
                        label,
                        path,
                        presentation,
                        tableName,
                        targetPageCode,
                        targetPageId));
                }
            }

            if (presentation == "inline" && string.IsNullOrWhiteSpace(ReadJsonString(rowAction, "microflowAlias")))
            {
                diagnostics.Add(PublishDiagnostic(
                    "designer.operation.inline_microflow_missing",
                    $"表格 {tableName} 的操作 {label} 使用内联保存时必须绑定保存微流。",
                    elementId,
                    actionId,
                    path,
                    "在交互区为该内联操作选择保存/执行微流。"));
            }

            actionIndex++;
        }
    }

    private static void CollectDesignerActionStepRequirements(
        string elementId,
        JsonElement element,
        IReadOnlySet<string> modalIds,
        List<ApplicationDevelopmentPublishDiagnosticDto> diagnostics,
        List<DesignerOperationTargetRequirement> targetRequirements)
    {
        if (!element.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var elementName = ReadJsonString(element, "name") ?? elementId;
        var eventIndex = 0;
        foreach (var eventAction in events.EnumerateArray())
        {
            if (eventAction.ValueKind != JsonValueKind.Object ||
                !eventAction.TryGetProperty("steps", out var steps) ||
                steps.ValueKind != JsonValueKind.Array)
            {
                eventIndex++;
                continue;
            }

            var stepIndex = 0;
            foreach (var step in steps.EnumerateArray())
            {
                if (step.ValueKind != JsonValueKind.Object)
                {
                    stepIndex++;
                    continue;
                }

                var stepType = ReadJsonString(step, "type");
                if (stepType == "openModal")
                {
                    var modalConfig = step.TryGetProperty("config", out var modalConfigValue) && modalConfigValue.ValueKind == JsonValueKind.Object
                        ? modalConfigValue
                        : step;
                    var modalActionId = ReadJsonString(eventAction, "id") ?? $"event[{eventIndex}]";
                    var modalPath = $"elements.{elementId}.events[{eventIndex}].steps[{stepIndex}]";
                    var modalId = ReadJsonString(modalConfig, "modalId");
                    if (string.IsNullOrWhiteSpace(modalId))
                    {
                        diagnostics.Add(PublishDiagnostic(
                            "designer.action.modal_target_missing",
                            $"组件 {elementName} 的正式 openModal 动作必须声明 modalId。",
                            elementId,
                            modalActionId,
                            modalPath,
                            "在交互区为 openModal 动作选择目标弹框或抽屉。"));
                    }
                    else if (!modalIds.Contains(modalId))
                    {
                        diagnostics.Add(PublishDiagnostic(
                            "designer.action.modal_target_not_found",
                            $"组件 {elementName} 的 openModal 动作引用了不存在的弹框或抽屉：{modalId}。",
                            elementId,
                            modalActionId,
                            modalPath,
                            "在 DesignerDocument.modals 中声明该目标，或重新选择已声明的目标。"));
                    }

                    stepIndex++;
                    continue;
                }

                if (stepType != "openPageInvocation" && stepType != "openModal")
                {
                    stepIndex++;
                    continue;
                }

                var config = step.TryGetProperty("config", out var configValue) && configValue.ValueKind == JsonValueKind.Object
                    ? configValue
                    : step;
                var presentation = ReadJsonString(config, "presentation")?.Trim().ToLowerInvariant();
                if (presentation is not ApplicationDevelopmentPageTypes.Dialog and not ApplicationDevelopmentPageTypes.Drawer)
                {
                    stepIndex++;
                    continue;
                }

                var targetPageCode = ReadJsonString(config, "targetPageCode")
                    ?? ReadJsonString(config, "target")
                    ?? ReadJsonString(config, "modalId")
                    ?? ReadJsonString(config, "targetModalId");
                var targetPageId = ReadJsonString(config, "targetPageId");
                var actionId = ReadJsonString(eventAction, "id") ?? $"event[{eventIndex}]";
                var path = $"elements.{elementId}.events[{eventIndex}].steps[{stepIndex}]";
                if (string.IsNullOrWhiteSpace(targetPageCode) && string.IsNullOrWhiteSpace(targetPageId))
                {
                    diagnostics.Add(PublishDiagnostic(
                        "designer.action.target_page_missing",
                        $"组件 {elementName} 的页面调用必须绑定目标页面。",
                        elementId,
                        actionId,
                        path,
                        "在交互区为页面调用选择目标弹框页或抽屉页。"));
                }
                else
                {
                    targetRequirements.Add(new DesignerOperationTargetRequirement(
                        actionId,
                        elementId,
                        ReadJsonString(config, "targetPageType"),
                        ReadMappingTargets(config, "inputMappings"),
                        elementName,
                        path,
                        presentation,
                        elementName,
                        targetPageCode,
                        targetPageId));
                }

                stepIndex++;
            }

            eventIndex++;
        }
    }

    private static IReadOnlySet<string> ReadDesignerModalIds(JsonElement document)
    {
        if (!document.TryGetProperty("modals", out var modals) || modals.ValueKind != JsonValueKind.Array)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return modals.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => ReadJsonString(item, "id"))
            .OfType<string>()
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async Task ValidateDesignerTargetPagesAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity sourcePage,
        IReadOnlyList<DesignerOperationTargetRequirement> requirements,
        List<ApplicationDevelopmentPublishDiagnosticDto> diagnostics,
        CancellationToken cancellationToken)
    {
        if (requirements.Count == 0)
        {
            return;
        }

        var targetIds = requirements
            .Select(item => item.TargetPageId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var targetCodes = requirements
            .Select(item => item.TargetPageCode)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var targetPages = await db.Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item =>
                item.TenantId == workspace.TenantId &&
                item.AppCode == workspace.AppCode &&
                item.VersionId == sourcePage.VersionId &&
                (targetIds.Contains(item.Id) || targetCodes.Contains(item.PageCode)) &&
                !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var targetPageByCode = targetPages.ToDictionary(item => item.PageCode, StringComparer.OrdinalIgnoreCase);
        var targetPageById = targetPages.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in requirements)
        {
            var targetPage = !string.IsNullOrWhiteSpace(requirement.TargetPageId) &&
                targetPageById.TryGetValue(requirement.TargetPageId, out var byId)
                    ? byId
                    : !string.IsNullOrWhiteSpace(requirement.TargetPageCode) &&
                      targetPageByCode.TryGetValue(requirement.TargetPageCode, out var byCode)
                        ? byCode
                        : null;
            if (targetPage is null)
            {
                diagnostics.Add(PublishDiagnostic(
                    "designer.invocation.target_page_not_found",
                    $"页面调用绑定的目标页面不存在：{requirement.TargetPageId ?? requirement.TargetPageCode}",
                    requirement.ElementId,
                    requirement.ActionId,
                    requirement.Path,
                    "重新选择当前版本下存在的目标页面。"));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(requirement.TargetPageCode) &&
                !string.Equals(targetPage.PageCode, requirement.TargetPageCode, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(PublishDiagnostic(
                    "designer.invocation.target_reference_mismatch",
                    $"页面调用目标页 id 与编码不一致：{requirement.TargetPageId} / {requirement.TargetPageCode}",
                    requirement.ElementId,
                    requirement.ActionId,
                    requirement.Path,
                    "重新选择目标页面，避免复制旧配置导致 id/code 指向不同页面。"));
            }

            var actualPageType = NormalizePageType(targetPage.PageType);
            if (!string.Equals(actualPageType, requirement.Presentation, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(PublishDiagnostic(
                    "designer.invocation.target_type_mismatch",
                    $"目标页面 {targetPage.PageCode} 类型为 {actualPageType}，不能作为 {requirement.Presentation} 打开。",
                    requirement.ElementId,
                    requirement.ActionId,
                    requirement.Path,
                    "选择类型匹配的弹框页或抽屉页，或修改目标页面类型。"));
            }

            if (!string.IsNullOrWhiteSpace(requirement.DeclaredTargetPageType) &&
                !string.Equals(requirement.DeclaredTargetPageType, actualPageType, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(PublishDiagnostic(
                    "designer.invocation.target_schema_stale",
                    $"页面调用记录的目标页类型为 {requirement.DeclaredTargetPageType}，实际目标页类型为 {actualPageType}。",
                    requirement.ElementId,
                    requirement.ActionId,
                    requirement.Path,
                    "重新选择目标页面，刷新 targetPageType 和 schema 版本引用。",
                    "warning"));
            }

            foreach (var requiredInput in ReadPageParameters(targetPage.PageParametersJson)
                         .Where(item => item.Required && string.Equals(item.Direction, "input", StringComparison.OrdinalIgnoreCase)))
            {
                if (!requirement.InputMappingTargets.Contains(requiredInput.Code))
                {
                    diagnostics.Add(PublishDiagnostic(
                        "designer.invocation.required_input_missing",
                        $"页面调用目标页 {targetPage.PageCode} 的必填入参 {requiredInput.Code} 未映射。",
                        requirement.ElementId,
                        requirement.ActionId,
                        requirement.Path,
                        "在参数树中为该目标页入参配置 input mapping。"));
                }
            }
        }
    }

    private static HashSet<string> ReadInvocationMappingTargets(JsonElement value)
    {
        var targets = ReadMappingTargets(value, "inputMappings");
        if (value.TryGetProperty("invocation", out var invocation) && invocation.ValueKind == JsonValueKind.Object)
        {
            targets.UnionWith(ReadMappingTargets(invocation, "inputMappings"));
        }

        if (value.TryGetProperty("action", out var action) &&
            action.ValueKind == JsonValueKind.Object &&
            action.TryGetProperty("steps", out var steps) &&
            steps.ValueKind == JsonValueKind.Array)
        {
            foreach (var step in steps.EnumerateArray())
            {
                if (step.ValueKind != JsonValueKind.Object ||
                    !step.TryGetProperty("config", out var config) ||
                    config.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                targets.UnionWith(ReadMappingTargets(config, "inputMappings"));
            }
        }

        return targets;
    }

    private static HashSet<string> ReadMappingTargets(JsonElement value, string key)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!value.TryGetProperty(key, out var mappings) || mappings.ValueKind != JsonValueKind.Array)
        {
            return targets;
        }

        foreach (var mapping in mappings.EnumerateArray())
        {
            if (mapping.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var target = ReadJsonString(mapping, "target")
                ?? ReadJsonString(mapping, "targetVariable")
                ?? ReadJsonString(mapping, "targetPath");
            if (!string.IsNullOrWhiteSpace(target))
            {
                targets.Add(target.Trim());
            }
        }

        return targets;
    }

    private static ApplicationDevelopmentPublishDiagnosticDto PublishDiagnostic(
        string code,
        string message,
        string? elementId,
        string? actionId,
        string? path,
        string fixHint,
        string severity = "error") =>
        new()
        {
            ActionId = actionId,
            Code = code,
            ElementId = elementId,
            FixHint = fixHint,
            Message = message,
            Path = path,
            Severity = severity
        };

    private sealed record DesignerOperationTargetRequirement(
        string? ActionId,
        string ElementId,
        string? DeclaredTargetPageType,
        IReadOnlySet<string> InputMappingTargets,
        string Label,
        string Path,
        string Presentation,
        string TableName,
        string? TargetPageCode,
        string? TargetPageId);

    private static string? ReadInvocationString(JsonElement value, string key)
    {
        return value.TryGetProperty("invocation", out var invocation) && invocation.ValueKind == JsonValueKind.Object
            ? ReadJsonString(invocation, key)
            : null;
    }

    private static string? ReadJsonString(JsonElement value, string key)
    {
        return value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static void ValidateNoDeprecatedModelDirectBinding(string documentJson)
    {
        using var document = JsonDocument.Parse(documentJson);
        if (ContainsDeprecatedModelDirectBinding(document.RootElement))
        {
            throw new ValidationException(
                "页面包含已下线的模型直连配置，请改为微流绑定",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static bool ContainsDeprecatedModelDirectBinding(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object => ObjectContainsDeprecatedModelDirectBinding(value),
            JsonValueKind.Array => value.EnumerateArray().Any(ContainsDeprecatedModelDirectBinding),
            _ => false
        };
    }

    private static bool ObjectContainsDeprecatedModelDirectBinding(JsonElement value)
    {
        if (value.TryGetProperty("type", out var type) &&
            type.ValueKind == JsonValueKind.String &&
            DeprecatedDesignerActionTypes.Contains(type.GetString() ?? string.Empty))
        {
            return true;
        }

        if (value.TryGetProperty("sourceKind", out var sourceKind) &&
            sourceKind.ValueKind == JsonValueKind.String &&
            DeprecatedDesignerDatasetSourceKinds.Contains(sourceKind.GetString() ?? string.Empty))
        {
            return true;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (string.Equals(property.Name, "dataBinding", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Object &&
                DataBindingContainsDeprecatedModelDirectBinding(property.Value))
            {
                return true;
            }

            if (ContainsDeprecatedModelDirectBinding(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DataBindingContainsDeprecatedModelDirectBinding(JsonElement dataBinding)
    {
        foreach (var key in DeprecatedDesignerDataBindingKeys)
        {
            if (dataBinding.TryGetProperty(key, out var value) && HasMeaningfulJsonValue(value))
            {
                return true;
            }
        }

        return ContainsDeprecatedModelDirectBinding(dataBinding);
    }

    private static bool HasMeaningfulJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => true
        };
    }

    private async Task UpsertPreviewMenuAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity page,
        CancellationToken cancellationToken)
    {
        var previewMenuCode = page.PreviewMenuCode;
        if (string.IsNullOrWhiteSpace(previewMenuCode))
        {
            previewMenuCode = $"draft-preview:{page.PageCode}:{page.Id[..8]}";
            page.PreviewMenuCode = previewMenuCode;
        }

        var existing = (await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.MenuCode == previewMenuCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        var parentMenuCode = await ResolveDevelopmentMenuParentCodeAsync(
            db,
            workspace,
            page,
            "ApplicationDraftPreview",
            PermissionCodes.AppDevelopmentCenterDesignerPreview,
            cancellationToken);
        var previewPageCode = page.PageCode;
        if (existing is null)
        {
            existing = new SystemMenuEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                MenuName = $"{page.PageName}（预览）",
                MenuCode = previewMenuCode,
                ParentCode = parentMenuCode,
                RoutePath = BuildPreviewRoute(page, previewPageCode),
                ComponentName = "RuntimePage",
                PageCode = previewPageCode,
                ScopeType = "ApplicationDraftPreview",
                ConfigJson = ApplicationDataCenterJson.Serialize(new { previewPageId = page.Id, pageId = page.Id, versionId = page.VersionId, templateCode = page.TemplateCode }),
                MenuType = "Menu",
                SortOrder = page.SortOrder,
                Visible = true,
                PermissionCode = PermissionCodes.AppDevelopmentCenterDesignerPreview,
                Icon = "FileCode2",
                CreatedBy = workspace.UserId,
                CreatedTime = now,
                IsDeleted = false
            };
            await db.Insertable(existing).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            existing.MenuName = $"{page.PageName}（预览）";
            existing.ParentCode = parentMenuCode;
            existing.RoutePath = BuildPreviewRoute(page, previewPageCode);
            existing.ComponentName = "RuntimePage";
            existing.PageCode = previewPageCode;
            existing.ScopeType = "ApplicationDraftPreview";
            existing.ConfigJson = ApplicationDataCenterJson.Serialize(new { previewPageId = page.Id, pageId = page.Id, versionId = page.VersionId, templateCode = page.TemplateCode });
            existing.MenuType = "Menu";
            existing.SortOrder = page.SortOrder;
            existing.Visible = true;
            existing.PermissionCode = PermissionCodes.AppDevelopmentCenterDesignerPreview;
            existing.Icon = "FileCode2";
            existing.UpdatedBy = workspace.UserId;
            existing.UpdatedTime = now;
            await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(page.PreviewMenuCode))
        {
            page.PreviewMenuCode = previewMenuCode;
        }
    }

    private async Task<SystemMenuEntity> UpsertPublishedMenuAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity page,
        ApplicationDevelopmentPermissionConfigDto permissionConfig,
        string viewPermission,
        CancellationToken cancellationToken)
    {
        var menuCode = ResolvePublishedMenuCode(page.PageCode, permissionConfig);
        var parentMenuCode = await ResolveDevelopmentMenuParentCodeAsync(
            db,
            workspace,
            page,
            "ApplicationRuntime",
            viewPermission,
            cancellationToken);
        var menu = (await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.MenuCode == menuCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (menu is null)
        {
            menu = new SystemMenuEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                MenuName = string.IsNullOrWhiteSpace(permissionConfig.MenuName) ? page.PageName : permissionConfig.MenuName.Trim(),
                MenuCode = menuCode,
                ParentCode = parentMenuCode,
                RoutePath = BuildPublishedRoute(page.PageCode),
                ComponentName = "RuntimePage",
                PageCode = page.PageCode,
                ArtifactId = page.PublishedArtifactId,
                ScopeType = "ApplicationRuntime",
                ConfigJson = ApplicationDataCenterJson.Serialize(new { generatedBy = "application-development-center" }),
                MenuType = "Menu",
                SortOrder = page.SortOrder,
                Visible = true,
                PermissionCode = viewPermission,
                Icon = "Table2",
                CreatedBy = workspace.UserId,
                CreatedTime = now,
                IsDeleted = false
            };
            await db.Insertable(menu).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            var desiredMenuName = string.IsNullOrWhiteSpace(permissionConfig.MenuName) ? page.PageName : permissionConfig.MenuName.Trim();
            var desiredRoutePath = BuildPublishedRoute(page.PageCode);
            var desiredConfigJson = ApplicationDataCenterJson.Serialize(new { generatedBy = "application-development-center" });
            if (string.Equals(menu.MenuName, desiredMenuName, StringComparison.Ordinal) &&
                string.Equals(menu.ParentCode, parentMenuCode, StringComparison.Ordinal) &&
                string.Equals(menu.RoutePath, desiredRoutePath, StringComparison.Ordinal) &&
                string.Equals(menu.ComponentName, "RuntimePage", StringComparison.Ordinal) &&
                string.Equals(menu.PageCode, page.PageCode, StringComparison.Ordinal) &&
                string.Equals(menu.ArtifactId, page.PublishedArtifactId, StringComparison.Ordinal) &&
                string.Equals(menu.ScopeType, "ApplicationRuntime", StringComparison.Ordinal) &&
                string.Equals(menu.ConfigJson, desiredConfigJson, StringComparison.Ordinal) &&
                string.Equals(menu.MenuType, "Menu", StringComparison.Ordinal) &&
                menu.SortOrder == page.SortOrder &&
                menu.Visible &&
                string.Equals(menu.PermissionCode, viewPermission, StringComparison.Ordinal) &&
                string.Equals(menu.Icon, "Table2", StringComparison.Ordinal))
            {
                return menu;
            }

            menu.MenuName = desiredMenuName;
            menu.ParentCode = parentMenuCode;
            menu.RoutePath = desiredRoutePath;
            menu.ComponentName = "RuntimePage";
            menu.PageCode = page.PageCode;
            menu.ArtifactId = page.PublishedArtifactId;
            menu.ScopeType = "ApplicationRuntime";
            menu.ConfigJson = desiredConfigJson;
            menu.MenuType = "Menu";
            menu.SortOrder = page.SortOrder;
            menu.Visible = true;
            menu.PermissionCode = viewPermission;
            menu.Icon = "Table2";
            menu.UpdatedBy = workspace.UserId;
            menu.UpdatedTime = now;
            await db.Updateable(menu).ExecuteCommandAsync(cancellationToken);
        }

        return menu;
    }

    private async Task<string> ResolveDevelopmentMenuParentCodeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity page,
        string scopeType,
        string? permissionCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(page.ModuleId))
        {
            return DefaultPreviewParentCode;
        }

        var modules = await db.Queryable<ApplicationDevelopmentModuleEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.VersionId == page.VersionId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var byId = modules.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        if (!byId.TryGetValue(page.ModuleId, out var current))
        {
            return DefaultPreviewParentCode;
        }

        var chain = new List<ApplicationDevelopmentModuleEntity>();
        while (current is not null)
        {
            chain.Add(current);
            current = !string.IsNullOrWhiteSpace(current.ParentModuleId) && byId.TryGetValue(current.ParentModuleId, out var parent)
                ? parent
                : null;
        }

        chain.Reverse();
        var parentCode = DefaultPreviewParentCode;
        foreach (var module in chain)
        {
            parentCode = await UpsertDevelopmentModuleMenuAsync(db, workspace, module, scopeType, parentCode, permissionCode, cancellationToken);
        }

        return parentCode;
    }

    private async Task SyncApplicationRuntimeModuleSubtreeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentModuleEntity rootModule,
        string? previousRootModuleCode,
        CancellationToken cancellationToken)
    {
        var modules = await db.Queryable<ApplicationDevelopmentModuleEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.VersionId == rootModule.VersionId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var byId = modules.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        if (!byId.TryGetValue(rootModule.Id, out var currentRoot))
        {
            return;
        }

        var rootParentCode = await ResolveApplicationRuntimeModuleParentMenuCodeAsync(db, workspace, currentRoot, byId, cancellationToken);
        var rootMenuCode = await UpsertDevelopmentModuleMenuAsync(
            db,
            workspace,
            currentRoot,
            "ApplicationRuntime",
            rootParentCode,
            null,
            cancellationToken,
            string.IsNullOrWhiteSpace(previousRootModuleCode) || string.Equals(previousRootModuleCode, currentRoot.ModuleCode, StringComparison.OrdinalIgnoreCase)
                ? null
                : BuildApplicationRuntimeModuleMenuCode(previousRootModuleCode));
        await SyncApplicationRuntimeChildModuleMenusAsync(db, workspace, modules, currentRoot.Id, rootMenuCode, cancellationToken);
    }

    private static async Task<string> ResolveApplicationRuntimeModuleParentMenuCodeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentModuleEntity module,
        IReadOnlyDictionary<string, ApplicationDevelopmentModuleEntity> modulesById,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(module.ParentModuleId) || !modulesById.TryGetValue(module.ParentModuleId, out var parent))
        {
            return DefaultPreviewParentCode;
        }

        var parentParentCode = await ResolveApplicationRuntimeModuleParentMenuCodeAsync(db, workspace, parent, modulesById, cancellationToken);
        return await UpsertDevelopmentModuleMenuAsync(
            db,
            workspace,
            parent,
            "ApplicationRuntime",
            parentParentCode,
            null,
            cancellationToken);
    }

    private static async Task SyncApplicationRuntimeChildModuleMenusAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        IReadOnlyList<ApplicationDevelopmentModuleEntity> modules,
        string parentModuleId,
        string parentMenuCode,
        CancellationToken cancellationToken)
    {
        var children = modules
            .Where(item => string.Equals(item.ParentModuleId, parentModuleId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.ModuleName)
            .ToList();
        foreach (var child in children)
        {
            var childMenuCode = await UpsertDevelopmentModuleMenuAsync(
                db,
                workspace,
                child,
                "ApplicationRuntime",
                parentMenuCode,
                null,
                cancellationToken);
            await SyncApplicationRuntimeChildModuleMenusAsync(db, workspace, modules, child.Id, childMenuCode, cancellationToken);
        }
    }

    private static async Task SoftDeleteApplicationRuntimeModuleMenuAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentModuleEntity module,
        CancellationToken cancellationToken)
    {
        var menuCode = BuildDevelopmentModuleMenuCode(module, "ApplicationRuntime");
        var menu = (await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.MenuCode == menuCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (menu is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        menu.IsDeleted = true;
        menu.DeletedBy = workspace.UserId;
        menu.DeletedTime = now;
        menu.UpdatedBy = workspace.UserId;
        menu.UpdatedTime = now;
        await db.Updateable(menu).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task<string> UpsertDevelopmentModuleMenuAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentModuleEntity module,
        string scopeType,
        string parentCode,
        string? permissionCode,
        CancellationToken cancellationToken,
        string? previousMenuCode = null)
    {
        var menuCode = BuildDevelopmentModuleMenuCode(module, scopeType);
        var menu = (await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.MenuCode == menuCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (menu is null && !string.IsNullOrWhiteSpace(previousMenuCode))
        {
            menu = (await db.Queryable<SystemMenuEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.MenuCode == previousMenuCode && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();
        }

        var now = DateTime.UtcNow;
        if (menu is null)
        {
            menu = new SystemMenuEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                MenuName = module.ModuleName,
                MenuCode = menuCode,
                ParentCode = parentCode,
                RoutePath = null,
                ComponentName = null,
                PageCode = null,
                ScopeType = scopeType,
                ConfigJson = ApplicationDataCenterJson.Serialize(new { generatedBy = "application-development-center", moduleId = module.Id, moduleCode = module.ModuleCode }),
                MenuType = "Directory",
                SortOrder = module.SortOrder,
                Visible = true,
                PermissionCode = permissionCode,
                Icon = "FolderTree",
                CreatedBy = workspace.UserId,
                CreatedTime = now,
                IsDeleted = false
            };
            await db.Insertable(menu).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            menu.MenuName = module.ModuleName;
            menu.MenuCode = menuCode;
            menu.ParentCode = parentCode;
            menu.RoutePath = null;
            menu.ComponentName = null;
            menu.PageCode = null;
            menu.ScopeType = scopeType;
            menu.ConfigJson = ApplicationDataCenterJson.Serialize(new { generatedBy = "application-development-center", moduleId = module.Id, moduleCode = module.ModuleCode });
            menu.MenuType = "Directory";
            menu.SortOrder = module.SortOrder;
            menu.Visible = true;
            menu.PermissionCode = permissionCode;
            menu.Icon = "FolderTree";
            menu.UpdatedBy = workspace.UserId;
            menu.UpdatedTime = now;
            await db.Updateable(menu).ExecuteCommandAsync(cancellationToken);
        }

        return menuCode;
    }

    private static string BuildDevelopmentModuleMenuCode(ApplicationDevelopmentModuleEntity module, string scopeType) =>
        string.Equals(scopeType, "ApplicationDraftPreview", StringComparison.OrdinalIgnoreCase)
            ? $"draft-module:{module.Id[..8]}:{module.ModuleCode}"
            : BuildApplicationRuntimeModuleMenuCode(module.ModuleCode);

    private static string BuildApplicationRuntimeModuleMenuCode(string moduleCode) =>
        $"app-module:{moduleCode}";

    private static IReadOnlyList<string> BuildPermissionCodes(string pageCode, ApplicationDevelopmentPermissionConfigDto permissionConfig)
    {
        var permissions = new List<string> { PermissionCodes.BuildAppRuntimePagePermission(pageCode, "view") };
        if (permissionConfig.AllowAdd)
        {
            permissions.Add(PermissionCodes.BuildAppRuntimePagePermission(pageCode, "add"));
        }

        if (permissionConfig.AllowEdit)
        {
            permissions.Add(PermissionCodes.BuildAppRuntimePagePermission(pageCode, "edit"));
        }

        if (permissionConfig.AllowDelete)
        {
            permissions.Add(PermissionCodes.BuildAppRuntimePagePermission(pageCode, "delete"));
        }

        if (permissionConfig.AllowImport)
        {
            permissions.Add(PermissionCodes.BuildAppRuntimePagePermission(pageCode, "import"));
        }

        if (permissionConfig.AllowExport)
        {
            permissions.Add(PermissionCodes.BuildAppRuntimePagePermission(pageCode, "export"));
        }

        return permissions;
    }

    private static async Task UpsertPermissionCodesAsync(
        ISqlSugarClient db,
        IReadOnlyList<string> permissionCodes,
        string pageName,
        string userId,
        CancellationToken cancellationToken)
    {
        var existing = await db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => permissionCodes.Contains(item.PermissionCode))
            .ToListAsync(cancellationToken);
        var byCode = existing.ToDictionary(item => item.PermissionCode, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var inserts = new List<SystemPermissionCodeEntity>();
        var updates = new List<SystemPermissionCodeEntity>();
        foreach (var code in permissionCodes)
        {
            if (!byCode.TryGetValue(code, out var entity))
            {
                inserts.Add(new SystemPermissionCodeEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ModuleName = PermissionCodes.AppRuntimePageModuleName,
                    PermissionCode = code,
                    PermissionName = $"{pageName}{ResolveActionName(code)}",
                    IsEnabled = true,
                    CreatedBy = userId,
                    CreatedTime = now,
                    IsDeleted = false
                });
                continue;
            }

            entity.ModuleName = PermissionCodes.AppRuntimePageModuleName;
            entity.PermissionName = $"{pageName}{ResolveActionName(code)}";
            entity.IsEnabled = true;
            entity.IsDeleted = false;
            entity.DeletedBy = null;
            entity.DeletedTime = null;
            entity.UpdatedBy = userId;
            entity.UpdatedTime = now;
            updates.Add(entity);
        }

        if (inserts.Count > 0)
        {
            await db.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }

        if (updates.Count > 0)
        {
            await db.Updateable(updates).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task GrantRuntimePermissionsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        IReadOnlyList<string> permissionCodes,
        IReadOnlyList<string> roleCodes,
        CancellationToken cancellationToken)
    {
        var roles = await db.Queryable<SystemRoleEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted && item.IsEnabled && roleCodes.Contains(item.RoleCode))
            .ToListAsync(cancellationToken);
        var appAdmin = (await db.Queryable<SystemRoleEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && !item.IsDeleted && item.RoleCode == "app_admin")
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (appAdmin is not null && roles.All(item => item.Id != appAdmin.Id))
        {
            roles.Add(appAdmin);
        }

        if (roles.Count == 0)
        {
            return;
        }

        var permissions = await db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => permissionCodes.Contains(item.PermissionCode) && !item.IsDeleted && item.IsEnabled)
            .ToListAsync(cancellationToken);
        var permissionIds = permissions.Select(item => item.Id).ToArray();
        var roleIds = roles.Select(item => item.Id).ToArray();
        var existing = await db.Queryable<SystemRolePermissionEntity>()
            .Where(item => roleIds.Contains(item.RoleId) && permissionIds.Contains(item.PermissionCodeId))
            .ToListAsync(cancellationToken);
        var existingKeys = existing.ToDictionary(item => $"{item.RoleId}:{item.PermissionCodeId}", StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var inserts = new List<SystemRolePermissionEntity>();
        foreach (var role in roles)
        {
            foreach (var permissionId in permissionIds)
            {
                var key = $"{role.Id}:{permissionId}";
                if (existingKeys.TryGetValue(key, out var current))
                {
                    if (current.IsDeleted)
                    {
                        current.IsDeleted = false;
                        current.DeletedBy = null;
                        current.DeletedTime = null;
                        current.UpdatedBy = workspace.UserId;
                        current.UpdatedTime = now;
                    }

                    continue;
                }

                inserts.Add(new SystemRolePermissionEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    RoleId = role.Id,
                    PermissionCodeId = permissionId,
                    CreatedBy = workspace.UserId,
                    CreatedTime = now,
                    IsDeleted = false
                });
            }
        }

        var updates = existing.Where(item => item.UpdatedTime == now && item.UpdatedBy == workspace.UserId).ToList();
        if (inserts.Count > 0)
        {
            await db.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }

        if (updates.Count > 0)
        {
            await db.Updateable(updates).ExecuteCommandAsync(cancellationToken);
        }
    }

    private void EnsurePermissionConfigurationAccess(string currentJson, string requestedJson)
    {
        if (string.Equals(currentJson, requestedJson, StringComparison.Ordinal) || currentUser.HasAsterErpPermission(PermissionCodes.AppDevelopmentCenterDesignerPermissionEdit))
        {
            return;
        }

        throw new ValidationException("Permission configuration edit permission is required.", ErrorCodes.PermissionDenied);
    }

    private static string NormalizePermissionConfigJson(string value)
    {
        var config = ApplicationDataCenterJson.Deserialize<ApplicationDevelopmentPermissionConfigDto>(value)
            ?? new ApplicationDevelopmentPermissionConfigDto();
        config.MenuCode = NormalizeOptionalText(config.MenuCode);
        config.MenuName = NormalizeOptionalText(config.MenuName);
        config.ParentMenuCode = NormalizeOptionalText(config.ParentMenuCode);
        config.RoleCodes = config.RoleCodes
            .Where(roleCode => !string.IsNullOrWhiteSpace(roleCode))
            .Select(roleCode => roleCode.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return ApplicationDataCenterJson.Serialize(config);
    }

    private static string ResolvePublishedMenuCode(string pageCode, ApplicationDevelopmentPermissionConfigDto permissionConfig)
    {
        var configuredMenuCode = NormalizeOptionalText(permissionConfig.MenuCode);
        return ApplicationDataCenterCodePolicy.NormalizeCode(
            configuredMenuCode ?? $"{pageCode}-menu",
            "菜单编码");
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static ApplicationDevelopmentBusinessObjectDto MapBusinessObject(ApplicationBusinessObjectDesignEntity entity)
    {
        var fields = ReadBusinessObjectFields(entity.FieldsJson);
        var keyField = ResolveBusinessObjectKeyField(entity.KeyField, fields);
        return new ApplicationDevelopmentBusinessObjectDto
        {
            Id = entity.Id,
            PageId = entity.PageId,
            VersionId = entity.VersionId,
            PageCode = entity.PageCode,
            PageName = entity.PageName,
            ModuleId = entity.ModuleId,
            ModelCode = entity.ModelCode,
            ModelName = entity.ModelName,
            MenuCode = entity.MenuCode,
            DataSourceId = entity.DataSourceId,
            SourceTable = entity.SourceTable,
            ProviderKey = entity.ProviderKey,
            KeyField = keyField,
            ReadOnly = string.IsNullOrWhiteSpace(keyField),
            Warnings = string.IsNullOrWhiteSpace(keyField)
                ? ["未发现主键字段，发布后仅生成只读运行页面。"]
                : [],
            Fields = fields,
            Status = entity.Status,
            UpdatedTime = entity.UpdatedTime
        };
    }

    private async Task EnsureBusinessObjectScopeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string versionId,
        string? moduleId,
        CancellationToken cancellationToken)
    {
        await GetRequiredAsync<ApplicationDevelopmentVersionEntity>(db, workspace, versionId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            await GetRequiredAsync<ApplicationDevelopmentModuleEntity>(db, workspace, moduleId, cancellationToken);
        }
    }

    private static async Task EnsureUniqueBusinessObjectCodeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string modelCode,
        string? currentId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<ApplicationBusinessObjectDesignEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.ModelCode == modelCode && !item.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(currentId), item => item.Id != currentId)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException($"业务对象模型编码 {modelCode} 已存在，禁止覆盖其他业务对象", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static async Task EnsureUniqueMenuCodeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string menuCode,
        string? currentMenuId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.MenuCode == menuCode && !item.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(currentMenuId), item => item.Id != currentMenuId)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException($"菜单编码 {menuCode} 已存在，禁止静默覆盖", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static string NormalizeBusinessObjectMenuCode(string? value, string pageCode) =>
        ApplicationDataCenterCodePolicy.NormalizeCode(
            string.IsNullOrWhiteSpace(value) ? $"{pageCode}-menu" : value,
            "业务对象菜单编码");

    private static string BuildBusinessObjectPermissionConfig(string value, string menuCode)
    {
        var config = ApplicationDataCenterJson.Deserialize<ApplicationDevelopmentPermissionConfigDto>(value)
            ?? new ApplicationDevelopmentPermissionConfigDto();
        config.MenuCode = menuCode;
        return ApplicationDataCenterJson.Serialize(config);
    }

    private static string NormalizeProviderKey(string? value)
    {
        var providerKey = string.IsNullOrWhiteSpace(value) ? "application-data-center.sql-table" : value.Trim();
        return providerKey switch
        {
            "application-data-center.sql-table" or "application-data-center.file" => providerKey,
            _ => throw new ValidationException($"业务对象 Provider {providerKey} 未注册", ErrorCodes.ApplicationDataCenterInvalidConfig)
        };
    }

    private static List<ApplicationDevelopmentBusinessObjectFieldDto> NormalizeBusinessObjectFields(
        IEnumerable<ApplicationDevelopmentBusinessObjectFieldDto>? fields)
    {
        var result = new List<ApplicationDevelopmentBusinessObjectFieldDto>();
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 1;
        foreach (var field in fields ?? [])
        {
            var code = ApplicationDataCenterCodePolicy.NormalizeCode(field.FieldCode, "业务对象字段编码");
            if (!usedCodes.Add(code))
            {
                throw new ValidationException($"业务对象字段编码 {code} 重复", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            result.Add(new ApplicationDevelopmentBusinessObjectFieldDto
            {
                FieldCode = code,
                FieldName = ApplicationDataCenterCodePolicy.NormalizeName(
                    string.IsNullOrWhiteSpace(field.FieldName) ? code : field.FieldName,
                    "业务对象字段名称"),
                DataType = string.IsNullOrWhiteSpace(field.DataType) ? "text" : field.DataType.Trim(),
                Binding = string.IsNullOrWhiteSpace(field.Binding) ? code : field.Binding.Trim(),
                Visible = field.Visible,
                Queryable = field.Queryable,
                Sortable = field.Sortable,
                Exportable = field.Exportable,
                Writable = field.Writable,
                Required = field.Required,
                IsPrimaryKey = field.IsPrimaryKey,
                Order = field.Order > 0 ? field.Order : order
            });
            order++;
        }

        if (result.Count == 0)
        {
            throw new ValidationException("业务对象至少需要一个字段定义", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return result;
    }

    private static IReadOnlyList<ApplicationDevelopmentBusinessObjectFieldDto> ReadBusinessObjectFields(string value)
    {
        try
        {
            return NormalizeBusinessObjectFields(ApplicationDataCenterJson.Deserialize<List<ApplicationDevelopmentBusinessObjectFieldDto>>(value) ?? []);
        }
        catch (JsonException)
        {
            throw new ValidationException("业务对象字段定义不是有效 JSON", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static string? ResolveBusinessObjectKeyField(
        string? requested,
        IReadOnlyList<ApplicationDevelopmentBusinessObjectFieldDto> fields)
    {
        var normalized = NormalizeOptionalText(requested);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            if (fields.All(item => !item.FieldCode.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ValidationException("业务对象主键字段必须存在于字段定义中", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            return fields.First(item => item.FieldCode.Equals(normalized, StringComparison.OrdinalIgnoreCase)).FieldCode;
        }

        return fields.FirstOrDefault(item => item.IsPrimaryKey)?.FieldCode;
    }

    private static RuntimeDataFieldDefinition MapRuntimeField(ApplicationDevelopmentBusinessObjectFieldDto field) =>
        new()
        {
            FieldCode = field.FieldCode,
            FieldName = field.FieldName,
            DataType = field.DataType,
            Binding = field.Binding ?? field.FieldCode,
            Visible = field.Visible,
            Queryable = field.Queryable,
            Sortable = field.Sortable,
            Exportable = field.Exportable,
            Writable = field.Writable,
            Required = field.Required,
            Order = field.Order
        };

    private static string NormalizeBusinessObjectLayout(string? value, string pageCode, string pageName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ApplicationDevelopmentPageDraftDocumentFactory.Create(pageCode, pageName);
        }

        var document = JsonNode.Parse(value) as JsonObject
            ?? throw new ValidationException("业务对象设计草稿必须是 JSON 对象", ErrorCodes.ApplicationDataCenterInvalidConfig);
        return document.ToJsonString(ApplicationDataCenterJson.Options);
    }

    private string CompileBusinessObjectSchema(
        ApplicationDevelopmentPageEntity page,
        ApplicationBusinessObjectDesignEntity design,
        IReadOnlyList<ApplicationDevelopmentBusinessObjectFieldDto> fields,
        string documentJson,
        string pageType,
        IReadOnlyList<RuntimeDataFieldDefinition>? runtimeFields = null,
        string? keyField = null,
        bool readOnly = false,
        ApplicationDevelopmentPermissionConfigDto? permissionConfig = null) =>
        schemaCompiler.CompileSchema(
            page.PageCode,
            page.PageName,
            pageType,
            [],
            documentJson,
            permissionConfig is null ? design.PermissionConfigJson : ApplicationDataCenterJson.Serialize(permissionConfig),
            runtimeFields ?? fields.Select(MapRuntimeField).ToArray(),
            keyField,
            design.ModelCode,
            readOnly,
            createRuntimeCrudActions: !readOnly,
            createImportExport: !readOnly);

    private static IReadOnlyList<string> BuildBusinessObjectPermissionCodes(
        string pageCode,
        ApplicationDevelopmentPermissionConfigDto permissionConfig,
        bool readOnly)
    {
        var permissions = new List<string> { PermissionCodes.BuildAppRuntimePagePermission(pageCode, "view") };
        if (permissionConfig.AllowExport)
        {
            permissions.Add(PermissionCodes.BuildAppRuntimePagePermission(pageCode, "export"));
        }

        if (readOnly)
        {
            return permissions;
        }

        if (permissionConfig.AllowAdd) permissions.Add(PermissionCodes.BuildAppRuntimePagePermission(pageCode, "add"));
        if (permissionConfig.AllowEdit) permissions.Add(PermissionCodes.BuildAppRuntimePagePermission(pageCode, "edit"));
        if (permissionConfig.AllowDelete) permissions.Add(PermissionCodes.BuildAppRuntimePagePermission(pageCode, "delete"));
        if (permissionConfig.AllowImport) permissions.Add(PermissionCodes.BuildAppRuntimePagePermission(pageCode, "import"));
        return permissions;
    }

    private static async Task<SystemDataModelEntity> UpsertBusinessObjectDataModelAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationBusinessObjectDesignEntity design,
        string pageCode,
        IReadOnlyList<RuntimeDataFieldDefinition> fields,
        string? keyField,
        string schemaJson,
        bool readOnly,
        CancellationToken cancellationToken)
    {
        var model = (await db.Queryable<SystemDataModelEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.ModelCode == design.ModelCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var isNew = model is null;
        var now = DateTime.UtcNow;
        model ??= new SystemDataModelEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ModelCode = design.ModelCode,
            CreatedBy = workspace.UserId,
            CreatedTime = now,
            IsDeleted = false
        };
        model.ModelName = design.ModelName;
        model.ProviderKey = design.ProviderKey;
        model.KeyField = keyField ?? string.Empty;
        model.PermissionCode = PermissionCodes.BuildAppRuntimePagePermission(pageCode, "view");
        model.VersionNo = Math.Max(1, model.VersionNo + 1);
        model.Status = "Published";
        model.SchemaJson = schemaJson;
        model.UpdatedBy = workspace.UserId;
        model.UpdatedTime = now;
        if (isNew)
        {
            await db.Insertable(model).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await db.Updateable(model).ExecuteCommandAsync(cancellationToken);
        }

        return model;
    }

    private string NormalizeLayoutJson(string value)
    {
        return schemaValidator.ValidateDraft(value).ToJsonString(ApplicationDataCenterJson.Options);
    }

    private static async Task<string?> NormalizeParentPageIdAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string versionId,
        string? parentPageId,
        string? currentPageId,
        CancellationToken cancellationToken)
    {
        var normalized = ApplicationDataCenterCodePolicy.NormalizeOptional(parentPageId);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(currentPageId) &&
            string.Equals(normalized, currentPageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("父页面不能选择当前页面", ErrorCodes.ParameterInvalid);
        }

        var parent = (await db.Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item =>
                item.Id == normalized &&
                item.TenantId == workspace.TenantId &&
                item.AppCode == workspace.AppCode &&
                item.VersionId == versionId &&
                !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("父页面不存在或不属于当前版本", ErrorCodes.ParameterInvalid);

        if (!string.IsNullOrWhiteSpace(parent.ParentPageId) &&
            !string.IsNullOrWhiteSpace(currentPageId) &&
            string.Equals(parent.ParentPageId, currentPageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("父页面不能形成循环层级", ErrorCodes.ParameterInvalid);
        }

        return parent.Id;
    }

    private static string NormalizePageType(string? value)
    {
        var normalized = value?.Trim();
        return ApplicationDevelopmentPageTypes.IsValid(normalized)
            ? normalized!
            : ApplicationDevelopmentPageTypes.Standard;
    }

    private static List<ApplicationDevelopmentPageParameterDto> NormalizePageParameters(IEnumerable<ApplicationDevelopmentPageParameterDto>? parameters)
    {
        if (parameters is null)
        {
            return [];
        }

        var result = new List<ApplicationDevelopmentPageParameterDto>();
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            var code = ApplicationDataCenterCodePolicy.NormalizeCode(parameter.Code, "页面参数编码");
            if (!usedCodes.Add(code))
            {
                throw new ValidationException($"页面参数编码 {code} 重复", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var direction = parameter.Direction?.Trim().ToLowerInvariant();
            if (direction is not ("input" or "output"))
            {
                throw new ValidationException($"页面参数 {code} 的方向必须是 input 或 output", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            result.Add(new ApplicationDevelopmentPageParameterDto
            {
                Code = code,
                DefaultValue = parameter.DefaultValue,
                Direction = direction,
                Name = ApplicationDataCenterCodePolicy.NormalizeName(parameter.Name, "页面参数名称"),
                Required = parameter.Required,
                ValueType = NormalizePageParameterValueType(parameter.ValueType)
            });
        }

        return result;
    }

    private static string NormalizePageParameterValueType(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is "array" or "boolean" or "date" or "datetime" or "json" or "number" or "string"
            ? normalized
            : "string";
    }

    private static List<ApplicationDevelopmentPageParameterDto> ReadPageParameters(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return NormalizePageParameters(ApplicationDataCenterJson.Deserialize<List<ApplicationDevelopmentPageParameterDto>>(value) ?? []);
        }
        catch (JsonException)
        {
            return [];
        }
        catch (ValidationException)
        {
            return [];
        }
    }

    private static (string PageType, List<ApplicationDevelopmentPageParameterDto> Parameters) ReadDocumentPageMetadata(string documentJson)
    {
        var document = JsonNode.Parse(documentJson) as JsonObject
            ?? throw new ValidationException("DesignerDocument must be a JSON object", ErrorCodes.DesignerSchemaInvalid);
        var pageType = document["pageType"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(pageType))
        {
            throw new ValidationException("DesignerDocument pageType is required", ErrorCodes.DesignerSchemaInvalid);
        }

        var parametersJson = (document["pageParameters"] as JsonArray ?? new JsonArray())
            .ToJsonString(ApplicationDataCenterJson.Options);
        return (NormalizePageType(pageType), NormalizePageParameters(ReadPageParameters(parametersJson)));
    }

    private static string NormalizeVersionStatus(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Draft" : value.Trim();

    private static string NormalizeResourceStatus(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Draft" : value.Trim();

    private static string NormalizeResourceType(string value) =>
        ApplicationDataCenterCodePolicy.NormalizeCode(value, "资源类型");

    private static string NormalizeSharedResourceJson(string value) =>
        ApplicationDataCenterJson.NormalizeObjectJson(value, "共享资源配置");

    private static string NormalizeTemplateCode(string value) =>
        string.IsNullOrWhiteSpace(value) ? "query-list" : value.Trim();

    internal static string NormalizeDesignerMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LatestDesignerMode;
        }

        if (string.Equals(value.Trim(), LatestDesignerMode, StringComparison.OrdinalIgnoreCase))
        {
            return LatestDesignerMode;
        }

        throw new ValidationException(
            $"DesignerMode '{value.Trim()}' is not supported; the only supported mode is '{LatestDesignerMode}'.",
            ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static string BuildPreviewRoute(ApplicationDevelopmentPageEntity page, string? pageCode = null) =>
        $"/pages/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(pageCode) ? page.PageCode : pageCode)}?previewPageId={Uri.EscapeDataString(page.Id)}";

    private static string BuildPublishedRoute(string pageCode) =>
        $"/pages/{Uri.EscapeDataString(pageCode)}";

    private static string ResolveActionName(string code)
    {
        var action = code.Split(':').LastOrDefault()?.ToLowerInvariant();
        return action switch
        {
            "view" => "查看",
            "add" => "新增",
            "edit" => "编辑",
            "delete" => "删除",
            "import" => "导入",
            "export" => "导出",
            _ => "操作"
        };
    }

    private static string? ReadConfigString(IReadOnlyDictionary<string, object?>? config, string key)
    {
        if (config is null || !config.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element => element.ToString(),
            _ => value.ToString()
        };
    }

    private static bool ReadConfigBool(IReadOnlyDictionary<string, object?> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool flag => flag,
            JsonElement element when element.ValueKind == JsonValueKind.True => true,
            JsonElement element when element.ValueKind == JsonValueKind.False => false,
            JsonElement element when element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var parsed) => parsed,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => false
        };
    }

    private static void SetConfigValue(Dictionary<string, object?> config, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            config.Remove(key);
            return;
        }

        config[key] = value.Trim();
    }

}
