using AsterERP.Contracts.System.Printing;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Printing;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.System.Printing;

public sealed class SystemPrintTemplateService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    PrintWorkspaceResolver workspaceResolver,
    PrintTargetCatalog targetCatalog)
{
    public async Task<IReadOnlyList<PrintTemplateListItemResponse>> GetPageAsync(
        string? keyword,
        string? menuCode,
        string? scene,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var query = databaseAccessor.GetCurrentDb().Queryable<SystemPrintTemplateEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(item =>
                item.Name.Contains(normalizedKeyword) ||
                item.TemplateCode.Contains(normalizedKeyword));
        }

        if (!string.IsNullOrWhiteSpace(menuCode))
        {
            var normalizedMenuCode = menuCode.Trim();
            query = query.Where(item => item.MenuCode == normalizedMenuCode);
        }

        if (!string.IsNullOrWhiteSpace(scene))
        {
            var normalizedScene = scene.Trim().ToLowerInvariant();
            query = query.Where(item => item.Scene == normalizedScene);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(item => item.Status == normalizedStatus);
        }

        var items = await query
            .OrderBy(item => item.IsDefault, OrderByType.Desc)
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);

        return await MapListAsync(items, cancellationToken);
    }

    public async Task<IReadOnlyList<PrintTemplateListItemResponse>> GetOptionsAsync(
        string menuCode,
        string scene,
        CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var normalizedMenuCode = menuCode.Trim();
        var normalizedScene = scene.Trim().ToLowerInvariant();

        var items = await databaseAccessor.GetCurrentDb().Queryable<SystemPrintTemplateEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                item.MenuCode == normalizedMenuCode &&
                item.Scene == normalizedScene &&
                item.Status == "Published")
            .OrderBy(item => item.IsDefault, OrderByType.Desc)
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);

        if (items.Count > 0)
        {
            return await MapListAsync(items, cancellationToken);
        }

        var drafts = await databaseAccessor.GetCurrentDb().Queryable<SystemPrintTemplateEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                item.MenuCode == normalizedMenuCode &&
                item.Scene == normalizedScene)
            .OrderBy(item => item.IsDefault, OrderByType.Desc)
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);

        return await MapListAsync(drafts, cancellationToken);
    }

    public async Task<PrintTemplateDetailResponse> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    public async Task<PrintTemplateDetailResponse> UpsertAsync(
        PrintTemplateUpsertRequest request,
        string? routeMenuCodeOverride = null,
        string? routeSceneOverride = null,
        CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var normalizedMenuCode = NormalizeRequired(routeMenuCodeOverride ?? request.MenuCode, "菜单编码");
        var normalizedScene = NormalizeRequired(routeSceneOverride ?? request.Scene, "打印场景").ToLowerInvariant();
        var definition = targetCatalog.GetRequiredDefinition(normalizedMenuCode);
        targetCatalog.GetRequiredScene(definition, normalizedScene);

        var normalizedName = NormalizeRequired(request.Name, "模板名称");
        var templateCode = NormalizeTemplateCode(request.TemplateCode, normalizedMenuCode, normalizedScene, normalizedName);
        var now = DateTime.UtcNow;
        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? null
            : await databaseAccessor.GetCurrentDb().Queryable<SystemPrintTemplateEntity>()
                .Where(item =>
                    item.Id == request.Id &&
                    !item.IsDeleted &&
                    item.TenantId == scope.TenantId &&
                    item.AppCode == scope.AppCode)
                .FirstAsync(cancellationToken);

        if (entity is null)
        {
            entity = new SystemPrintTemplateEntity
            {
                TenantId = scope.TenantId,
                AppCode = scope.AppCode,
                MenuCode = normalizedMenuCode,
                Scene = normalizedScene,
                TemplateCode = templateCode,
                Name = normalizedName,
                Status = "Draft",
                IsDefault = false,
                DraftDataJson = PrintJsonNodeMapper.Serialize(request.Data),
                DraftExtJson = PrintJsonNodeMapper.Serialize(request.Ext),
                DraftPermissionsJson = PrintJsonNodeMapper.Serialize(request.Permissions),
                Remark = NormalizeOptional(request.Remark),
                CreatedBy = scope.UserId,
                UpdatedBy = scope.UserId,
                UpdatedTime = now
            };

            await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
            return await MapDetailAsync(entity, cancellationToken);
        }

        entity.MenuCode = normalizedMenuCode;
        entity.Scene = normalizedScene;
        entity.Name = normalizedName;
        entity.TemplateCode = templateCode;
        entity.DraftDataJson = PrintJsonNodeMapper.Serialize(request.Data);
        entity.DraftExtJson = PrintJsonNodeMapper.Serialize(request.Ext);
        entity.DraftPermissionsJson = PrintJsonNodeMapper.Serialize(request.Permissions);
        entity.Remark = NormalizeOptional(request.Remark);
        entity.Status = string.IsNullOrWhiteSpace(entity.PublishedDataJson) ? "Draft" : "Published";
        entity.UpdatedBy = scope.UserId;
        entity.UpdatedTime = now;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var entity = await GetRequiredAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedBy = scope.UserId;
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedBy = scope.UserId;
        entity.UpdatedTime = entity.DeletedTime;
        entity.IsDefault = false;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<PrintTemplateDetailResponse> PublishAsync(string id, CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var entity = await GetRequiredAsync(id, cancellationToken);
        entity.PublishedDataJson = entity.DraftDataJson;
        entity.PublishedExtJson = entity.DraftExtJson;
        entity.PublishedPermissionsJson = entity.DraftPermissionsJson;
        entity.Status = "Published";
        entity.UpdatedBy = scope.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    public async Task<PrintTemplateDetailResponse> SetDefaultAsync(string id, CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var entity = await GetRequiredAsync(id, cancellationToken);
        var siblings = await databaseAccessor.GetCurrentDb().Queryable<SystemPrintTemplateEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                item.MenuCode == entity.MenuCode &&
                item.Scene == entity.Scene)
            .ToListAsync(cancellationToken);

        foreach (var sibling in siblings)
        {
            sibling.IsDefault = sibling.Id == entity.Id;
            sibling.UpdatedBy = scope.UserId;
            sibling.UpdatedTime = DateTime.UtcNow;
        }

        await databaseAccessor.GetCurrentDb().Updateable(siblings).ExecuteCommandAsync(cancellationToken);
        entity.IsDefault = true;
        return await MapDetailAsync(entity, cancellationToken);
    }

    public async Task<SystemPrintTemplateEntity> GetRuntimeTemplateAsync(
        string menuCode,
        string scene,
        string? templateId,
        CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        if (!string.IsNullOrWhiteSpace(templateId))
        {
            return await GetRequiredAsync(templateId.Trim(), cancellationToken);
        }

        var normalizedMenuCode = menuCode.Trim();
        var normalizedScene = scene.Trim().ToLowerInvariant();
        var published = await databaseAccessor.GetCurrentDb().Queryable<SystemPrintTemplateEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                item.MenuCode == normalizedMenuCode &&
                item.Scene == normalizedScene &&
                item.Status == "Published")
            .OrderBy(item => item.IsDefault, OrderByType.Desc)
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .FirstAsync(cancellationToken);

        if (published is not null)
        {
            return published;
        }

        var draft = await databaseAccessor.GetCurrentDb().Queryable<SystemPrintTemplateEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                item.MenuCode == normalizedMenuCode &&
                item.Scene == normalizedScene)
            .OrderBy(item => item.IsDefault, OrderByType.Desc)
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .FirstAsync(cancellationToken);

        return draft ?? throw new ValidationException($"当前菜单场景还没有可用打印模板：{menuCode}/{scene}");
    }

    private async Task<SystemPrintTemplateEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var entity = await databaseAccessor.GetCurrentDb().Queryable<SystemPrintTemplateEntity>()
            .Where(item =>
                item.Id == id &&
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode)
            .FirstAsync(cancellationToken);

        return entity ?? throw new KeyNotFoundException($"未找到打印模板：{id}");
    }

    private async Task<IReadOnlyList<PrintTemplateListItemResponse>> MapListAsync(
        IReadOnlyList<SystemPrintTemplateEntity> entities,
        CancellationToken cancellationToken)
    {
        var menuMap = await LoadMenuMapAsync(entities.Select(item => item.MenuCode).Distinct().ToList(), cancellationToken);
        return entities
            .Select(entity =>
            {
                menuMap.TryGetValue(entity.MenuCode, out var menu);
                return new PrintTemplateListItemResponse(
                    entity.Id,
                    entity.Name,
                    entity.MenuCode,
                    menu?.MenuName ?? entity.MenuCode,
                    menu?.RoutePath,
                    entity.Scene,
                    entity.TemplateCode,
                    entity.Status,
                    entity.IsDefault,
                    PrintJsonNodeMapper.ToUnixMilliseconds(entity.CreatedTime, entity.UpdatedTime),
                    PrintJsonNodeMapper.Deserialize(entity.DraftExtJson),
                    PrintJsonNodeMapper.Deserialize(entity.DraftPermissionsJson),
                    entity.Remark);
            })
            .ToList();
    }

    private async Task<PrintTemplateDetailResponse> MapDetailAsync(
        SystemPrintTemplateEntity entity,
        CancellationToken cancellationToken)
    {
        var menuMap = await LoadMenuMapAsync([entity.MenuCode], cancellationToken);
        menuMap.TryGetValue(entity.MenuCode, out var menu);
        return new PrintTemplateDetailResponse(
            entity.Id,
            entity.Name,
            entity.MenuCode,
            menu?.MenuName ?? entity.MenuCode,
            menu?.RoutePath,
            entity.Scene,
            entity.TemplateCode,
            entity.Status,
            entity.IsDefault,
            PrintJsonNodeMapper.ToUnixMilliseconds(entity.CreatedTime, entity.UpdatedTime),
            PrintJsonNodeMapper.Deserialize(entity.DraftDataJson),
            PrintJsonNodeMapper.Deserialize(entity.DraftExtJson),
            PrintJsonNodeMapper.Deserialize(entity.DraftPermissionsJson),
            entity.Remark);
    }

    private async Task<Dictionary<string, SystemMenuEntity>> LoadMenuMapAsync(
        IReadOnlyList<string> menuCodes,
        CancellationToken cancellationToken)
    {
        if (menuCodes.Count == 0)
        {
            return new Dictionary<string, SystemMenuEntity>(StringComparer.OrdinalIgnoreCase);
        }

        var scope = workspaceResolver.GetRequiredCurrent();
        var menus = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                menuCodes.Contains(item.MenuCode))
            .ToListAsync(cancellationToken);

        return menus.ToDictionary(item => item.MenuCode, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeRequired(string? value, string displayName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{displayName}不能为空。");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeTemplateCode(string? templateCode, string menuCode, string scene, string name)
    {
        var source = string.IsNullOrWhiteSpace(templateCode) ? name : templateCode;
        var normalized = new string(source
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = $"{menuCode}-{scene}";
        }

        return normalized.Length > 96 ? normalized[..96] : normalized;
    }
}

