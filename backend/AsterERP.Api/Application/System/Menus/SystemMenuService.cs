using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.Menus;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Domain.System.Menus;
using AsterERP.Api.Application.Platform;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using SqlSugar;

namespace AsterERP.Api.Application.System.Menus;

public sealed class SystemMenuService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    PlatformAccessGuard accessGuard) : ISystemMenuService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemMenuEntity>, OrderByType, ISugarQueryable<SystemMenuEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemMenuEntity>, OrderByType, ISugarQueryable<SystemMenuEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["appCode"] = (query, order) => query.OrderBy(item => item.AppCode, order),
            ["componentName"] = (query, order) => query.OrderBy(item => item.ComponentName, order),
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["menuCode"] = (query, order) => query.OrderBy(item => item.MenuCode, order),
            ["menuName"] = (query, order) => query.OrderBy(item => item.MenuName, order),
            ["menuType"] = (query, order) => query.OrderBy(item => item.MenuType, order),
            ["pageCode"] = (query, order) => query.OrderBy(item => item.PageCode, order),
            ["parentCode"] = (query, order) => query.OrderBy(item => item.ParentCode, order),
            ["permissionCode"] = (query, order) => query.OrderBy(item => item.PermissionCode, order),
            ["routePath"] = (query, order) => query.OrderBy(item => item.RoutePath, order),
            ["sortOrder"] = (query, order) => query.OrderBy(item => item.SortOrder, order),
            ["tenantId"] = (query, order) => query.OrderBy(item => item.TenantId, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order),
            ["visible"] = (query, order) => query.OrderBy(item => item.Visible, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemMenuEntity>, GridFilter, ISugarQueryable<SystemMenuEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemMenuEntity>, GridFilter, ISugarQueryable<SystemMenuEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["appCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.AppCode),
            ["componentName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ComponentName),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["menuCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.MenuCode),
            ["menuName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.MenuName),
            ["menuType"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.MenuType),
            ["pageCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.PageCode),
            ["parentCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ParentCode),
            ["permissionCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.PermissionCode),
            ["routePath"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.RoutePath),
            ["sortOrder"] = (query, filter) => GridFilterApplier.ApplyInt32(query, filter, item => item.SortOrder),
            ["tenantId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.TenantId),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime),
            ["visible"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, item => item.Visible)
        };

    public async Task<GridPageResult<MenuListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(gridQuery.TenantId, gridQuery.AppCode, cancellationToken);
        var keyword = gridQuery.Keyword?.Trim();
        var menuType = NormalizeOptional(gridQuery.MenuType);
        var status = NormalizeOptional(gridQuery.Status);
        var parentCode = NormalizeOptional(gridQuery.ParentId);
        var query = databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == scope.TenantId && item.AppCode == scope.AppCode);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                item.MenuName.Contains(keyword) ||
                item.MenuCode.Contains(keyword) ||
                (item.RoutePath != null && item.RoutePath.Contains(keyword)) ||
                (item.PageCode != null && item.PageCode.Contains(keyword)) ||
                (item.PermissionCode != null && item.PermissionCode.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(menuType))
        {
            query = query.Where(item => item.MenuType == menuType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var visible = NormalizeVisibleStatus(status);
            query = query.Where(item => item.Visible == visible);
        }

        if (!string.IsNullOrWhiteSpace(parentCode))
        {
            if (gridQuery.IncludeDescendants)
            {
                var allMenus = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
                    .Where(item => !item.IsDeleted && item.TenantId == scope.TenantId && item.AppCode == scope.AppCode)
                    .ToListAsync(cancellationToken);
                var menuCodes = ResolveMenuCodesWithDescendants(allMenus, [parentCode]);
                query = menuCodes.Count == 0
                    ? query.Where(item => item.Id == "__none__")
                    : query.Where(item => menuCodes.Contains(item.MenuCode));
            }
            else
            {
                query = query.Where(item => item.ParentCode == parentCode);
            }
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, Filterers);

        var totalCount = new RefAsync<int>();
        var items = await GridSortApplier
            .Apply(query, gridQuery.Sorts, Sorters, ApplyDefaultSort)
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, totalCount);

        var parentNames = (await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == scope.TenantId && item.AppCode == scope.AppCode)
            .ToListAsync(cancellationToken))
            .ToDictionary(item => item.MenuCode, item => item.MenuName, StringComparer.OrdinalIgnoreCase);

        return new GridPageResult<MenuListItemResponse>
        {
            Total = totalCount.Value,
            Items = items
                .Select(item => Map(
                    item,
                    parentNames.TryGetValue(item.ParentCode ?? string.Empty, out var parentName) ? parentName : null))
                .ToList()
        };
    }

    public async Task<IReadOnlyList<MenuTreeNodeResponse>> GetTreeAsync(GridQuery? gridQuery = null, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(gridQuery?.TenantId, gridQuery?.AppCode, cancellationToken);
        var menus = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == scope.TenantId && item.AppCode == scope.AppCode)
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        return BuildTree(menus);
    }

    public async Task<MenuListItemResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        var parentName = string.IsNullOrWhiteSpace(entity.ParentCode)
            ? null
            : (await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
                .Where(item => item.TenantId == entity.TenantId && item.AppCode == entity.AppCode && item.MenuCode == entity.ParentCode && !item.IsDeleted)
                .Select(item => item.MenuName)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();

        return Map(entity, parentName);
    }

    public async Task<IReadOnlyList<MenuTreeNodeResponse>> GetVisibleTreeAsync(
        IReadOnlyList<string> permissionCodes,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveExistingScopeAsync(tenantId, appCode, cancellationToken);
        var menuList = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                item.Visible &&
                item.MenuType != "Button" &&
                item.MenuType != "按钮")
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        var tree = BuildTree(menuList);
        if (permissionCodes.Contains("*", StringComparer.OrdinalIgnoreCase))
        {
            return tree;
        }

        return FilterTree(tree, permissionCodes);
    }

    public async Task<MenuListItemResponse> CreateAsync(MenuUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(request.TenantId, request.AppCode, cancellationToken);
        MenuDomainPolicy.EnsureUpsertRequest(request.MenuName, request.MenuCode, request.MenuType);
        await EnsureUniqueCodeAsync(request.MenuCode, null, scope, cancellationToken);
        await EnsureParentExistsAsync(request.ParentCode, request.MenuCode, scope, cancellationToken);

        var entity = new SystemMenuEntity
        {
            TenantId = scope.TenantId,
            AppCode = scope.AppCode,
            MenuName = request.MenuName.Trim(),
            MenuCode = request.MenuCode.Trim(),
            ParentCode = NormalizeOptional(request.ParentCode),
            RoutePath = NormalizeOptional(request.RoutePath),
            ComponentName = NormalizeOptional(request.ComponentName),
            PageCode = NormalizeOptional(request.PageCode),
            ArtifactId = NormalizeOptional(request.ArtifactId),
            ScopeType = NormalizeOptional(request.ScopeType),
            ConfigJson = NormalizeOptional(request.ConfigJson),
            MenuType = request.MenuType.Trim(),
            SortOrder = request.SortOrder,
            Visible = request.Visible,
            PermissionCode = NormalizeOptional(request.PermissionCode),
            Icon = NormalizeOptional(request.Icon),
            Remark = NormalizeOptional(request.Remark)
        };

        await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity, null);
    }

    public async Task<MenuListItemResponse> UpdateAsync(string id, MenuUpsertRequest request, CancellationToken cancellationToken = default)
    {
        MenuDomainPolicy.EnsureUpsertRequest(request.MenuName, request.MenuCode, request.MenuType);

        var entity = await GetRequiredAsync(id, cancellationToken);
        var scope = await ResolveScopeAsync(entity.TenantId, entity.AppCode, cancellationToken);
        if ((!string.IsNullOrWhiteSpace(request.TenantId) && !string.Equals(request.TenantId.Trim(), scope.TenantId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(request.AppCode) && !string.Equals(request.AppCode.Trim(), scope.AppCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("不能修改菜单归属工作区");
        }

        var originalCode = entity.MenuCode;
        await EnsureUniqueCodeAsync(request.MenuCode, id, scope, cancellationToken);
        await EnsureParentExistsAsync(request.ParentCode, request.MenuCode, scope, cancellationToken);
        MenuDomainPolicy.EnsureNotSelfParent(request.ParentCode, request.MenuCode);

        await unitOfWork.ExecuteAsync(async () =>
        {
            entity.MenuName = request.MenuName.Trim();
            entity.MenuCode = request.MenuCode.Trim();
            entity.ParentCode = NormalizeOptional(request.ParentCode);
            entity.RoutePath = NormalizeOptional(request.RoutePath);
            entity.ComponentName = NormalizeOptional(request.ComponentName);
            entity.PageCode = NormalizeOptional(request.PageCode);
            entity.ArtifactId = NormalizeOptional(request.ArtifactId);
            entity.ScopeType = NormalizeOptional(request.ScopeType);
            entity.ConfigJson = NormalizeOptional(request.ConfigJson);
            entity.MenuType = request.MenuType.Trim();
            entity.SortOrder = request.SortOrder;
            entity.Visible = request.Visible;
            entity.PermissionCode = NormalizeOptional(request.PermissionCode);
            entity.Icon = NormalizeOptional(request.Icon);
            entity.Remark = NormalizeOptional(request.Remark);
            entity.UpdatedTime = DateTime.UtcNow;

            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);

            if (!string.Equals(originalCode, entity.MenuCode, StringComparison.OrdinalIgnoreCase))
            {
                var children = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
                    .Where(item => !item.IsDeleted && item.TenantId == scope.TenantId && item.AppCode == scope.AppCode && item.ParentCode == originalCode)
                    .ToListAsync(cancellationToken);

                if (children.Count > 0)
                {
                    foreach (var child in children)
                    {
                        child.ParentCode = entity.MenuCode;
                        child.UpdatedTime = entity.UpdatedTime;
                    }

                    await databaseAccessor.GetCurrentDb().Updateable(children).ExecuteCommandAsync(cancellationToken);
                }
            }
        }, cancellationToken);

        return Map(entity, null);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await BatchDeleteAsync([id], cancellationToken);
    }

    public async Task BatchDeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        var normalizedIds = NormalizeIds(ids);
        if (normalizedIds.Count == 0)
        {
            return;
        }

        var allMenus = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var rootMenus = allMenus.Where(item => normalizedIds.Contains(item.Id)).ToList();

        if (rootMenus.Count != normalizedIds.Count)
        {
            throw new NotFoundException("菜单不存在", ErrorCodes.MenuNotFound);
        }

        var rootScope = await ResolveScopeAsync(rootMenus[0].TenantId, rootMenus[0].AppCode, cancellationToken);
        if (rootMenus.Any(item => !string.Equals(item.TenantId, rootScope.TenantId, StringComparison.OrdinalIgnoreCase) || !string.Equals(item.AppCode, rootScope.AppCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("批量删除菜单必须属于同一工作区");
        }

        var scopedMenus = allMenus
            .Where(item => item.TenantId == rootScope.TenantId && item.AppCode == rootScope.AppCode)
            .ToList();
        var menuCodes = ResolveMenuCodesWithDescendants(scopedMenus, rootMenus.Select(item => item.MenuCode).ToList());
        var menusToDelete = scopedMenus.Where(item => menuCodes.Contains(item.MenuCode)).ToList();
        var permissionCodes = menusToDelete
            .Select(item => NormalizeOptional(item.PermissionCode))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var permissionCodeIds = permissionCodes.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemPermissionCodeEntity>()
                .Where(item => permissionCodes.Contains(item.PermissionCode) && !item.IsDeleted)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
        var rolePermissionMappings = permissionCodeIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemRolePermissionEntity>()
                .Where(item => permissionCodeIds.Contains(item.PermissionCodeId) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var deletedTime = DateTime.UtcNow;

        foreach (var menu in menusToDelete)
        {
            menu.IsDeleted = true;
            menu.Visible = false;
            menu.DeletedTime = deletedTime;
            menu.UpdatedTime = deletedTime;
        }

        foreach (var mapping in rolePermissionMappings)
        {
            mapping.IsDeleted = true;
            mapping.DeletedTime = deletedTime;
            mapping.UpdatedTime = deletedTime;
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable(menusToDelete)
                .UpdateColumns(item => new { item.IsDeleted, item.Visible, item.DeletedTime, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);

            if (rolePermissionMappings.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(rolePermissionMappings)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }
        }, cancellationToken);
    }

    public async Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default)
    {
        var normalizedIds = NormalizeIds(ids);
        if (normalizedIds.Count == 0)
        {
            return;
        }

        var entities = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        if (entities.Count != normalizedIds.Count)
        {
            throw new NotFoundException("菜单不存在", ErrorCodes.MenuNotFound);
        }

        var scope = await ResolveScopeAsync(entities[0].TenantId, entities[0].AppCode, cancellationToken);
        if (entities.Any(item => !string.Equals(item.TenantId, scope.TenantId, StringComparison.OrdinalIgnoreCase) || !string.Equals(item.AppCode, scope.AppCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("批量更新菜单状态必须属于同一工作区");
        }

        var visible = NormalizeVisibleStatus(status);
        var updatedTime = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            entity.Visible = visible;
            entity.UpdatedTime = updatedTime;
        }

        await databaseAccessor.GetCurrentDb().Updateable(entities)
            .UpdateColumns(item => new { item.Visible, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<SystemMenuEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var entity = (await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return entity ?? throw new NotFoundException("菜单不存在", ErrorCodes.MenuNotFound);
    }

    private async Task EnsureUniqueCodeAsync(string menuCode, string? currentId, WorkspaceScope scope, CancellationToken cancellationToken)
    {
        var normalizedCode = menuCode.Trim();
        var exists = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item =>
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                item.MenuCode == normalizedCode &&
                item.Id != (currentId ?? string.Empty) &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new ValidationException("菜单编码已存在", ErrorCodes.DuplicateMenuCode);
        }
    }

    private async Task EnsureParentExistsAsync(string? parentCode, string currentCode, WorkspaceScope scope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentCode))
        {
            return;
        }

        MenuDomainPolicy.EnsureNotSelfParent(parentCode, currentCode);

        var exists = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == scope.TenantId && item.AppCode == scope.AppCode && item.MenuCode == parentCode.Trim() && !item.IsDeleted)
            .AnyAsync(cancellationToken);

        if (!exists)
        {
            throw new ValidationException("上级菜单不存在");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static List<string> NormalizeIds(IReadOnlyList<string> ids)
    {
        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool NormalizeVisibleStatus(string status)
    {
        return status.Trim().Equals("Enabled", StringComparison.OrdinalIgnoreCase);
    }

    private static ISugarQueryable<SystemMenuEntity> ApplyDefaultSort(ISugarQueryable<SystemMenuEntity> query) =>
        query.OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc);

    private static MenuListItemResponse Map(SystemMenuEntity entity, string? parentName)
    {
        return new MenuListItemResponse(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.MenuName,
            entity.MenuCode,
            entity.ParentCode,
            parentName,
            entity.RoutePath,
            entity.ComponentName,
            entity.PageCode,
            entity.ArtifactId,
            entity.ScopeType,
            entity.ConfigJson,
            entity.MenuType,
            entity.SortOrder,
            entity.Visible,
            entity.PermissionCode,
            entity.Icon,
            entity.Remark);
    }

    private static IReadOnlyList<MenuTreeNodeResponse> BuildTree(IReadOnlyList<SystemMenuEntity> menus)
    {
        var lookup = menus.ToDictionary(item => item.MenuCode, item => new MenuTreeNodeResponse(
            item.Id,
            item.TenantId,
            item.AppCode,
            item.MenuName,
            item.MenuCode,
            item.RoutePath,
            item.ComponentName,
            item.PageCode,
            item.ArtifactId,
            item.ScopeType,
            item.ConfigJson,
            item.MenuType,
            item.SortOrder,
            item.Visible,
            item.PermissionCode,
            item.Icon,
            []), StringComparer.OrdinalIgnoreCase);

        var nodes = lookup.Values.ToDictionary(node => node.MenuCode, node => new TreeNodeBuilder(node));

        foreach (var menu in menus)
        {
            if (string.IsNullOrWhiteSpace(menu.ParentCode))
            {
                continue;
            }

            if (!nodes.TryGetValue(menu.MenuCode, out var node))
            {
                continue;
            }

            if (nodes.TryGetValue(menu.ParentCode, out var parent))
            {
                parent.Children.Add(node);
            }
        }

        return menus
            .Where(item => string.IsNullOrWhiteSpace(item.ParentCode) || !lookup.ContainsKey(item.ParentCode))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.CreatedTime)
            .Select(item => nodes[item.MenuCode].ToResponse())
            .ToList();
    }

    private static IReadOnlyList<MenuTreeNodeResponse> FilterTree(
        IReadOnlyList<MenuTreeNodeResponse> nodes,
        IReadOnlyCollection<string> permissionCodes)
    {
        var filtered = new List<MenuTreeNodeResponse>();

        foreach (var node in nodes)
        {
            var childNodes = FilterTree(node.Children, permissionCodes);
            var isPermitted =
                string.IsNullOrWhiteSpace(node.PermissionCode) ||
                permissionCodes.Contains(node.PermissionCode, StringComparer.OrdinalIgnoreCase);

            if (isPermitted || childNodes.Count > 0)
            {
                filtered.Add(node with { Children = childNodes.ToList() });
            }
        }

        return filtered;
    }

    private static List<string> ResolveMenuCodesWithDescendants(
        IReadOnlyList<SystemMenuEntity> menus,
        IReadOnlyList<string> rootCodes)
    {
        var normalizedRootCodes = rootCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var resolved = new HashSet<string>(normalizedRootCodes, StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(normalizedRootCodes);

        while (queue.Count > 0)
        {
            var currentCode = queue.Dequeue();
            foreach (var childCode in menus
                         .Where(item => string.Equals(item.ParentCode, currentCode, StringComparison.OrdinalIgnoreCase))
                         .Select(item => item.MenuCode))
            {
                if (resolved.Add(childCode))
                {
                    queue.Enqueue(childCode);
                }
            }
        }

        return resolved.ToList();
    }

    private sealed class TreeNodeBuilder(MenuTreeNodeResponse value)
    {
        public MenuTreeNodeResponse Value { get; } = value;

        public List<TreeNodeBuilder> Children { get; } = [];

        public MenuTreeNodeResponse ToResponse()
        {
            var children = Children
                .OrderBy(item => item.Value.SortOrder)
                .ThenBy(item => item.Value.MenuName)
                .Select(item => item.ToResponse())
                .ToList();

            return Value with { Children = children };
        }
    }

    private async Task<WorkspaceScope> ResolveScopeAsync(string? tenantId, string? appCode, CancellationToken cancellationToken)
    {
        var normalizedTenantId = NormalizeOptional(tenantId) ?? currentUser.GetAsterErpTenantId();
        var normalizedAppCode = NormalizeOptional(appCode)?.ToUpperInvariant() ?? currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(normalizedTenantId) || string.IsNullOrWhiteSpace(normalizedAppCode))
        {
            throw new ValidationException("请先选择租户应用工作区", ErrorCodes.PermissionDenied);
        }

        var isCurrentWorkspace =
            string.Equals(normalizedTenantId, currentUser.GetAsterErpTenantId(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalizedAppCode, currentUser.GetAsterErpAppCode(), StringComparison.OrdinalIgnoreCase);
        if (!isCurrentWorkspace)
        {
            accessGuard.EnsurePlatformAdmin();
        }

        var tenantAppExists = await databaseAccessor.MainDb.Queryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
                (tenantApp, tenant, app) => tenantApp.TenantId == tenant.Id && tenantApp.AppCode == app.AppCode)
            .Where((tenantApp, tenant, app) =>
                tenantApp.TenantId == normalizedTenantId &&
                tenantApp.AppCode == normalizedAppCode &&
                !tenantApp.IsDeleted &&
                !tenant.IsDeleted &&
                !app.IsDeleted)
            .AnyAsync(cancellationToken);
        if (!tenantAppExists)
        {
            throw new ValidationException("租户应用不存在");
        }

        return new WorkspaceScope(normalizedTenantId, normalizedAppCode);
    }

    private async Task<WorkspaceScope> ResolveExistingScopeAsync(string tenantId, string appCode, CancellationToken cancellationToken)
    {
        var normalizedTenantId = tenantId.Trim();
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedTenantId) || string.IsNullOrWhiteSpace(normalizedAppCode))
        {
            throw new ValidationException("租户应用工作区不能为空", ErrorCodes.PermissionDenied);
        }

        var tenantAppExists = await databaseAccessor.MainDb.Queryable<SystemTenantAppEntity, SystemTenantEntity, SystemApplicationEntity>(
                (tenantApp, tenant, app) => tenantApp.TenantId == tenant.Id && tenantApp.AppCode == app.AppCode)
            .Where((tenantApp, tenant, app) =>
                tenantApp.TenantId == normalizedTenantId &&
                tenantApp.AppCode == normalizedAppCode &&
                !tenantApp.IsDeleted &&
                !tenant.IsDeleted &&
                !app.IsDeleted)
            .AnyAsync(cancellationToken);
        if (!tenantAppExists)
        {
            throw new ValidationException("租户应用不存在");
        }

        return new WorkspaceScope(normalizedTenantId, normalizedAppCode);
    }

    private sealed record WorkspaceScope(string TenantId, string AppCode);
}

