using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.System.Menus;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Auth;

public sealed class WorkspaceMenuReader(
    ISqlSugarClient db,
    ApplicationDatabaseBindingResolver bindingResolver,
    IApplicationDatabaseConnectionFactory connectionFactory,
    ApplicationDatabaseSchemaInitializer schemaInitializer) : IWorkspaceMenuReader
{
    public async Task<IReadOnlyList<MenuTreeNodeResponse>> GetVisibleTreeAsync(
        SystemUserEntity user,
        IReadOnlyList<string> permissionCodes,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = tenantId.Trim();
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        if (!string.Equals(normalizedAppCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            return await GetApplicationMenuTreeAsync(
                user,
                permissionCodes,
                normalizedTenantId,
                normalizedAppCode,
                cancellationToken);
        }

        var menuList = await db.Queryable<SystemMenuEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == normalizedTenantId &&
                item.AppCode == normalizedAppCode &&
                item.Visible &&
                item.MenuType != "Button" &&
                item.MenuType != "按钮")
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        var tree = BuildTree(menuList);
        return permissionCodes.Contains("*", StringComparer.OrdinalIgnoreCase)
            ? tree
            : FilterTree(tree, permissionCodes);
    }

    private async Task<IReadOnlyList<MenuTreeNodeResponse>> GetApplicationMenuTreeAsync(
        SystemUserEntity user,
        IReadOnlyList<string> permissionCodes,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var tenantApp = (await db.Queryable<SystemTenantAppEntity>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                !item.IsDeleted &&
                item.Status == "Enabled")
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("当前应用工作区不存在或已停用", ErrorCodes.PermissionDenied);

        var binding = bindingResolver.Resolve(tenantApp.ConfigJson, tenantId, appCode)
            ?? throw new ValidationException("请先绑定应用数据库", ErrorCodes.ApplicationDatabaseNotBound);

        ISqlSugarClient? appDb = null;
        try
        {
            appDb = connectionFactory.Create(binding);
            await schemaInitializer.EnsureBaselineAsync(appDb, tenantId, appCode, user, cancellationToken, tenantApp.ConfigJson);

            var menuList = await appDb.Queryable<SystemMenuEntity>()
                .Where(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.Visible &&
                    item.MenuType != "Button" &&
                    item.MenuType != "按钮")
                .OrderBy(item => item.SortOrder, OrderByType.Asc)
                .ToListAsync(cancellationToken);

            var tree = BuildTree(menuList);
            return permissionCodes.Contains("*", StringComparer.OrdinalIgnoreCase)
                ? tree
                : FilterTree(tree, permissionCodes);
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            throw new ValidationException("应用数据库连接失败，请检查数据库绑定", ErrorCodes.ApplicationDatabaseConnectionFailed);
        }
        finally
        {
            if (appDb is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
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
            if (string.IsNullOrWhiteSpace(menu.ParentCode) ||
                !nodes.TryGetValue(menu.MenuCode, out var node) ||
                !nodes.TryGetValue(menu.ParentCode, out var parent))
            {
                continue;
            }

            parent.Children.Add(node);
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
}
