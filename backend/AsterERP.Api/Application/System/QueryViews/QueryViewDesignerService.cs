using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.QueryViews;
using AsterERP.Api.Infrastructure.QueryViews;
using AsterERP.Api.Modules.System.QueryViews;
using SqlSugar;

namespace AsterERP.Api.Application.System.QueryViews;

public sealed class QueryViewDesignerService(
    ICurrentUser currentUser,
    IWorkspaceDatabaseAccessor databaseAccessor,
    IQueryViewRuntimeService runtimeService) : IQueryViewDesignerService
{
    public async Task<IReadOnlyList<QueryViewDesignerResponse>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var views = await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewDefinitionEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.ModuleCode)
            .OrderBy(item => item.ViewCode)
            .ToListAsync(cancellationToken);

        return views.Select(Map).ToList();
    }

    public async Task<QueryViewDesignerResponse> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return Map(await GetRequiredAsync(id, cancellationToken));
    }

    public async Task<QueryViewDesignerResponse> CreateAsync(QueryViewDesignerSaveRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureUniqueViewCodeAsync(request.ViewCode, null, cancellationToken);
        ValidateDesign(request);
        var entity = new SystemQueryViewDefinitionEntity
        {
            ViewCode = request.ViewCode.Trim(),
            ViewName = request.ViewName.Trim(),
            ModuleCode = request.ModuleCode.Trim(),
            MenuCode = NormalizeOptional(request.MenuCode),
            ViewType = NormalizeViewType(request.ViewType),
            IsDefault = request.IsDefault,
            IsEnabled = request.IsEnabled,
            DefaultPageSize = NormalizeDefaultPageSize(request.DefaultPageSize),
            MaxPageSize = NormalizeMaxPageSize(request.MaxPageSize),
            Status = "Draft",
            VersionNo = 0,
            DesignJson = QueryViewDesignJson.Serialize(request),
            Remark = NormalizeOptional(request.Remark)
        };

        await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<QueryViewDesignerResponse> UpdateAsync(string id, QueryViewDesignerSaveRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        await EnsureUniqueViewCodeAsync(request.ViewCode, id, cancellationToken);
        ValidateDesign(request);

        entity.ViewCode = request.ViewCode.Trim();
        entity.ViewName = request.ViewName.Trim();
        entity.ModuleCode = request.ModuleCode.Trim();
        entity.MenuCode = NormalizeOptional(request.MenuCode);
        entity.ViewType = NormalizeViewType(request.ViewType);
        entity.IsDefault = request.IsDefault;
        entity.IsEnabled = request.IsEnabled;
        entity.DefaultPageSize = NormalizeDefaultPageSize(request.DefaultPageSize);
        entity.MaxPageSize = NormalizeMaxPageSize(request.MaxPageSize);
        entity.DesignJson = QueryViewDesignJson.Serialize(request);
        entity.Remark = NormalizeOptional(request.Remark);
        entity.UpdatedTime = DateTime.UtcNow;

        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<QueryViewPlanPreviewResponse> PreviewPlanAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        EnsureOrmRuntimeSupported(entity.ViewCode);
        return new QueryViewPlanPreviewResponse("SqlSugar.Queryable", entity.ViewCode);
    }

    public async Task<QueryViewDataPreviewResponse> PreviewDataAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        EnsureOrmRuntimeSupported(entity.ViewCode);
        var result = await runtimeService.QueryAsync(
            entity.ViewCode,
            new QueryViewQueryRequest(1, 20, [], []),
            cancellationToken);
        return new QueryViewDataPreviewResponse(result.Rows, result.Rows.Count);
    }

    public async Task<QueryViewPublishResponse> PublishAsync(string id, QueryViewPublishRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        EnsureOrmRuntimeSupported(entity.ViewCode);
        var design = QueryViewDesignJson.Deserialize(entity.DesignJson);
        var nextVersion = Math.Max(1, entity.VersionNo + 1);
        var stableProvider = entity.ViewCode;
        var versionProvider = $"{entity.ViewCode}:v{nextVersion}";

        entity.VersionNo = nextVersion;
        entity.Status = "Published";
        entity.UpdatedTime = DateTime.UtcNow;
        entity.DefaultPageSize = NormalizeDefaultPageSize(design.DefaultPageSize);
        entity.MaxPageSize = NormalizeMaxPageSize(design.MaxPageSize);
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);

        await UpsertRuntimeAsync(entity, stableProvider, versionProvider, nextVersion, cancellationToken);
        await InsertPublishLogAsync(entity.Id, nextVersion, stableProvider, versionProvider, "Publish", request.Remark, null, cancellationToken);

        return new QueryViewPublishResponse(entity.Id, entity.ViewCode, nextVersion, stableProvider, versionProvider, "Published");
    }

    public async Task<QueryViewPublishResponse> RollbackAsync(string id, QueryViewRollbackRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        if (request.TargetVersion <= 0 || request.TargetVersion > entity.VersionNo)
        {
            throw new ValidationException("回滚版本无效", ErrorCodes.QueryViewInvalid);
        }

        EnsureOrmRuntimeSupported(entity.ViewCode);
        var stableProvider = entity.ViewCode;
        var versionProvider = $"{entity.ViewCode}:v{request.TargetVersion}";
        await UpsertRuntimeAsync(entity, stableProvider, versionProvider, request.TargetVersion, cancellationToken);
        await InsertPublishLogAsync(entity.Id, request.TargetVersion, stableProvider, versionProvider, "Rollback", $"回滚到 v{request.TargetVersion}", null, cancellationToken);
        return new QueryViewPublishResponse(entity.Id, entity.ViewCode, request.TargetVersion, stableProvider, versionProvider, "Rollback");
    }

    public async Task<IReadOnlyList<QueryViewPublishLogResponse>> GetPublishLogsAsync(string? viewId, CancellationToken cancellationToken = default)
    {
        var query = databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewPublishLogEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(viewId))
        {
            query = query.Where(item => item.ViewId == viewId.Trim());
        }

        var logs = await query.OrderBy(item => item.PublishedTime, OrderByType.Desc).ToListAsync(cancellationToken);
        return logs.Select(item => new QueryViewPublishLogResponse(
            item.Id,
            item.ViewId,
            item.VersionNo,
            item.StableViewName,
            item.VersionViewName,
            item.Action,
            item.PublishStatus,
            item.ErrorMessage,
            item.Remark,
            item.PublishedBy,
            item.PublishedTime)).ToList();
    }

    private static void ValidateDesign(QueryViewDesignerSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ViewCode) || string.IsNullOrWhiteSpace(request.ViewName))
        {
            throw new ValidationException("视图编码和名称不能为空", ErrorCodes.QueryViewInvalid);
        }

        QueryViewNames.NormalizeViewCode(request.ViewCode);
        _ = QueryViewDesignJson.Deserialize(QueryViewDesignJson.Serialize(request));
        if (request.Projections.All(item => !string.Equals(item.FieldAlias, "id", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("查询视图必须包含隐藏主键 id", ErrorCodes.QueryViewInvalid);
        }

        EnsureOrmRuntimeSupported(request.ViewCode);
    }

    private static void EnsureOrmRuntimeSupported(string viewCode)
    {
        if (!SupportedProviders.Contains(QueryViewNames.NormalizeViewCode(viewCode)))
        {
            throw new ValidationException("当前仅允许发布已注册 ORM 运行时提供器的查询视图", ErrorCodes.QueryViewInvalid);
        }
    }

    private async Task UpsertRuntimeAsync(
        SystemQueryViewDefinitionEntity entity,
        string stableProvider,
        string versionProvider,
        int versionNo,
        CancellationToken cancellationToken)
    {
        var runtime = (await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewRuntimeEntity>()
            .Where(item => item.ViewId == entity.Id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (runtime is null)
        {
            runtime = new SystemQueryViewRuntimeEntity
            {
                ViewId = entity.Id,
                StableViewName = stableProvider,
                CurrentVersionViewName = versionProvider,
                CurrentVersionNo = versionNo,
                LastCheckTime = DateTime.UtcNow,
                HealthStatus = "healthy"
            };
            await databaseAccessor.GetCurrentDb().Insertable(runtime).ExecuteCommandAsync(cancellationToken);
            return;
        }

        runtime.StableViewName = stableProvider;
        runtime.CurrentVersionViewName = versionProvider;
        runtime.CurrentVersionNo = versionNo;
        runtime.LastCheckTime = DateTime.UtcNow;
        runtime.HealthStatus = "healthy";
        runtime.LastError = null;
        runtime.UpdatedTime = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Updateable(runtime).ExecuteCommandAsync(cancellationToken);
    }

    private async Task InsertPublishLogAsync(
        string viewId,
        int versionNo,
        string stableProvider,
        string versionProvider,
        string action,
        string? remark,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var log = new SystemQueryViewPublishLogEntity
        {
            ViewId = viewId,
            VersionNo = versionNo,
            StableViewName = stableProvider,
            VersionViewName = versionProvider,
            Action = action,
            PublishStatus = string.IsNullOrWhiteSpace(errorMessage) ? "Success" : "Failed",
            ErrorMessage = NormalizeOptional(errorMessage),
            Remark = NormalizeOptional(remark),
            PublishedBy = currentUser.GetAsterErpUserId(),
            PublishedTime = DateTime.UtcNow
        };
        await databaseAccessor.GetCurrentDb().Insertable(log).ExecuteCommandAsync(cancellationToken);
    }

    private async Task EnsureUniqueViewCodeAsync(string viewCode, string? currentId, CancellationToken cancellationToken)
    {
        QueryViewNames.NormalizeViewCode(viewCode);
        var normalized = viewCode.Trim();
        var exists = await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewDefinitionEntity>()
            .Where(item => item.ViewCode == normalized && item.Id != (currentId ?? string.Empty) && !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException("视图编码已存在", ErrorCodes.QueryViewInvalid);
        }
    }

    private async Task<SystemQueryViewDefinitionEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var entity = (await databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewDefinitionEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return entity ?? throw new ValidationException("查询视图不存在", ErrorCodes.QueryViewNotFound);
    }

    private static QueryViewDesignerResponse Map(SystemQueryViewDefinitionEntity entity)
    {
        var design = QueryViewDesignJson.Deserialize(entity.DesignJson);
        return new QueryViewDesignerResponse(
            entity.Id,
            entity.ViewName,
            entity.ViewCode,
            entity.ModuleCode,
            entity.MenuCode,
            entity.ViewType,
            entity.IsDefault,
            entity.IsEnabled,
            entity.VersionNo,
            entity.DefaultPageSize,
            entity.MaxPageSize,
            entity.Status,
            entity.Remark,
            design.Tables,
            design.Relations,
            design.Projections,
            design.Conditions,
            design.Sorts);
    }

    private static string NormalizeViewType(string viewType)
    {
        var normalized = viewType.Trim().ToLowerInvariant();
        return normalized is "list" or "tree" or "report"
            ? normalized
            : throw new ValidationException("视图类型仅支持 list/tree/report");
    }

    private static int NormalizeDefaultPageSize(int pageSize)
    {
        return pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
    }

    private static int NormalizeMaxPageSize(int pageSize)
    {
        return pageSize <= 0 ? 100 : Math.Min(pageSize, 5000);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "system_user_default",
        "system_dept_tree",
        "system_position_default",
        "system_menu_tree",
        "system_role_default",
        "system_dict_type",
        "system_dict_item"
    };
}

