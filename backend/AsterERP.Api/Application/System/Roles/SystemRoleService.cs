using AsterERP.Api.Application.System.Menus;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Application.Platform;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.Menus;
using AsterERP.Contracts.System.Roles;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Domain.System.Roles;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.System.Roles;

public sealed class SystemRoleService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ISystemMenuService systemMenuService,
    ICurrentUser currentUser,
    PlatformAccessGuard accessGuard,
    IUnitOfWork unitOfWork) : ISystemRoleService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemRoleEntity>, OrderByType, ISugarQueryable<SystemRoleEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemRoleEntity>, OrderByType, ISugarQueryable<SystemRoleEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["appCode"] = (query, order) => query.OrderBy(item => item.AppCode, order),
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["dataScope"] = (query, order) => query.OrderBy(item => item.DataScope, order),
            ["isEnabled"] = (query, order) => query.OrderBy(item => item.IsEnabled, order),
            ["roleCode"] = (query, order) => query.OrderBy(item => item.RoleCode, order),
            ["roleName"] = (query, order) => query.OrderBy(item => item.RoleName, order),
            ["tenantId"] = (query, order) => query.OrderBy(item => item.TenantId, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemRoleEntity>, GridFilter, ISugarQueryable<SystemRoleEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemRoleEntity>, GridFilter, ISugarQueryable<SystemRoleEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["appCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.AppCode),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["dataScope"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.DataScope),
            ["isEnabled"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, item => item.IsEnabled),
            ["roleCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.RoleCode),
            ["roleName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.RoleName),
            ["tenantId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.TenantId),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime)
        };

    public async Task<GridPageResult<RoleListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(gridQuery.TenantId, gridQuery.AppCode, cancellationToken);
        var keyword = gridQuery.Keyword?.Trim();
        var status = gridQuery.Status?.Trim();
        var query = databaseAccessor.GetCurrentDb().Queryable<SystemRoleEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == scope.TenantId && item.AppCode == scope.AppCode);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item => item.RoleName.Contains(keyword) || item.RoleCode.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var isEnabled = status.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
            query = query.Where(item => item.IsEnabled == isEnabled);
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, Filterers);

        var totalCount = new RefAsync<int>();
        var roles = await GridSortApplier
            .Apply(query, gridQuery.Sorts, Sorters, ApplyDefaultSort)
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, totalCount);

        var roleIds = roles.Select(item => item.Id).ToList();
        var userCountByRole = roleIds.Count == 0
            ? new Dictionary<string, int>()
            : (await databaseAccessor.GetCurrentDb().Queryable<SystemUserRoleEntity>()
                .Where(item => roleIds.Contains(item.RoleId) && !item.IsDeleted)
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.RoleId)
                .ToDictionary(group => group.Key, group => group.Count());
        var permissionCountByRole = roleIds.Count == 0
            ? new Dictionary<string, int>()
            : (await databaseAccessor.GetCurrentDb().Queryable<SystemRolePermissionEntity>()
                .Where(item => roleIds.Contains(item.RoleId) && !item.IsDeleted)
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.RoleId)
                .ToDictionary(group => group.Key, group => group.Count());

        return new GridPageResult<RoleListItemResponse>
        {
            Total = totalCount.Value,
            Items = roles.Select(role => new RoleListItemResponse(
                role.Id,
                role.TenantId,
                role.AppCode,
                role.RoleName,
                role.RoleCode,
                role.DataScope,
                role.IsEnabled,
                userCountByRole.TryGetValue(role.Id, out var userCount) ? userCount : 0,
                permissionCountByRole.TryGetValue(role.Id, out var permissionCount) ? permissionCount : 0,
                role.Remark)).ToList()
        };
    }

    public async Task<IReadOnlyList<RolePermissionCatalogItemResponse>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default)
    {
        return (await databaseAccessor.GetCurrentDb().Queryable<SystemPermissionCodeEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled)
            .ToListAsync(cancellationToken))
            .OrderBy(item => item.ModuleName)
            .ThenBy(item => item.PermissionCode)
            .Select(item => new RolePermissionCatalogItemResponse(item.ModuleName, item.PermissionCode, item.PermissionName, item.IsEnabled))
            .ToList();
    }

    public async Task<RoleListItemResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        var role = await GetRequiredAsync(id, cancellationToken);
        var userCount = await databaseAccessor.GetCurrentDb().Queryable<SystemUserRoleEntity>()
            .Where(item => item.RoleId == role.Id && !item.IsDeleted)
            .CountAsync(cancellationToken);
        var permissionCount = await databaseAccessor.GetCurrentDb().Queryable<SystemRolePermissionEntity>()
            .Where(item => item.RoleId == role.Id && !item.IsDeleted)
            .CountAsync(cancellationToken);
        return Map(role, userCount, permissionCount);
    }

    public async Task<IReadOnlyList<string>> GetRolePermissionCodesAsync(string roleId, CancellationToken cancellationToken = default)
    {
        return await GetPermissionCodesByRoleIdAsync(roleId, cancellationToken);
    }

    public async Task<IReadOnlyList<MenuTreeNodeResponse>> GetPermissionTreeAsync(GridQuery? gridQuery = null, CancellationToken cancellationToken = default)
    {
        return await systemMenuService.GetTreeAsync(gridQuery, cancellationToken);
    }

    public async Task<RoleListItemResponse> CreateAsync(RoleUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(request.TenantId, request.AppCode, cancellationToken);
        RoleDomainPolicy.EnsureUpsertRequest(request.RoleName, request.RoleCode);
        await EnsureUniqueCodeAsync(request.RoleCode, null, scope, cancellationToken);

        var entity = new SystemRoleEntity
        {
            TenantId = scope.TenantId,
            AppCode = scope.AppCode,
            RoleName = request.RoleName.Trim(),
            RoleCode = request.RoleCode.Trim(),
            DataScope = RoleDomainPolicy.NormalizeDataScope(request.DataScope),
            IsEnabled = request.IsEnabled,
            Remark = NormalizeOptional(request.Remark)
        };

        await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity, 0, 0);
    }

    public async Task<RoleListItemResponse> UpdateAsync(string id, RoleUpsertRequest request, CancellationToken cancellationToken = default)
    {
        RoleDomainPolicy.EnsureUpsertRequest(request.RoleName, request.RoleCode);
        var entity = await GetRequiredAsync(id, cancellationToken);
        var scope = await ResolveScopeAsync(entity.TenantId, entity.AppCode, cancellationToken);
        if ((!string.IsNullOrWhiteSpace(request.TenantId) && !string.Equals(request.TenantId.Trim(), scope.TenantId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(request.AppCode) && !string.Equals(request.AppCode.Trim(), scope.AppCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("不能修改角色归属工作区");
        }

        await EnsureUniqueCodeAsync(request.RoleCode, id, scope, cancellationToken);

        entity.RoleName = request.RoleName.Trim();
        entity.RoleCode = request.RoleCode.Trim();
        entity.DataScope = RoleDomainPolicy.NormalizeDataScope(request.DataScope);
        entity.IsEnabled = request.IsEnabled;
        entity.Remark = NormalizeOptional(request.Remark);
        entity.UpdatedTime = DateTime.UtcNow;

        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);

        return Map(entity, 0, 0);
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

        var roles = await databaseAccessor.GetCurrentDb().Queryable<SystemRoleEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        if (roles.Count != normalizedIds.Count)
        {
            throw new NotFoundException("角色不存在", ErrorCodes.RoleNotFound);
        }

        var deleteScope = await ResolveScopeAsync(roles[0].TenantId, roles[0].AppCode, cancellationToken);
        if (roles.Any(item => !string.Equals(item.TenantId, deleteScope.TenantId, StringComparison.OrdinalIgnoreCase) || !string.Equals(item.AppCode, deleteScope.AppCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("批量删除角色必须属于同一工作区");
        }

        var userRoleMappings = await databaseAccessor.GetCurrentDb().Queryable<SystemUserRoleEntity>()
            .Where(item => normalizedIds.Contains(item.RoleId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var rolePermissionMappings = await databaseAccessor.GetCurrentDb().Queryable<SystemRolePermissionEntity>()
            .Where(item => normalizedIds.Contains(item.RoleId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var deletedTime = DateTime.UtcNow;

        foreach (var role in roles)
        {
            role.IsDeleted = true;
            role.DeletedTime = deletedTime;
            role.UpdatedTime = deletedTime;
        }

        foreach (var mapping in userRoleMappings)
        {
            mapping.IsDeleted = true;
            mapping.DeletedTime = deletedTime;
            mapping.UpdatedTime = deletedTime;
        }

        foreach (var mapping in rolePermissionMappings)
        {
            mapping.IsDeleted = true;
            mapping.DeletedTime = deletedTime;
            mapping.UpdatedTime = deletedTime;
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable(roles)
                .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);

            if (userRoleMappings.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(userRoleMappings)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }

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

        var roles = await databaseAccessor.GetCurrentDb().Queryable<SystemRoleEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        if (roles.Count != normalizedIds.Count)
        {
            throw new NotFoundException("角色不存在", ErrorCodes.RoleNotFound);
        }

        var statusScope = await ResolveScopeAsync(roles[0].TenantId, roles[0].AppCode, cancellationToken);
        if (roles.Any(item => !string.Equals(item.TenantId, statusScope.TenantId, StringComparison.OrdinalIgnoreCase) || !string.Equals(item.AppCode, statusScope.AppCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("批量更新角色状态必须属于同一工作区");
        }

        var isEnabled = status.Trim().Equals("Enabled", StringComparison.OrdinalIgnoreCase);
        var updatedTime = DateTime.UtcNow;
        foreach (var role in roles)
        {
            role.IsEnabled = isEnabled;
            role.UpdatedTime = updatedTime;
        }

        await databaseAccessor.GetCurrentDb().Updateable(roles)
            .UpdateColumns(item => new { item.IsEnabled, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdatePermissionsAsync(string roleId, RolePermissionUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var role = await GetRequiredAsync(roleId, cancellationToken);
        var scope = await ResolveScopeAsync(role.TenantId, role.AppCode, cancellationToken);
        var normalizedCodes = request.PermissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var catalog = await databaseAccessor.GetCurrentDb().Queryable<SystemPermissionCodeEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled)
            .ToListAsync(cancellationToken);
        var invalidCodes = normalizedCodes
            .Where(code => !catalog.Any(item => string.Equals(item.PermissionCode, code, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var menuPermissionCodes = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item =>
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                !item.IsDeleted &&
                item.PermissionCode != null)
            .Select(item => item.PermissionCode!)
            .ToListAsync(cancellationToken);
        var invalidScopeCodes = normalizedCodes
            .Where(code => !menuPermissionCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (invalidCodes.Count > 0)
        {
            throw new ValidationException($"权限码不存在: {string.Join(", ", invalidCodes)}");
        }

        if (invalidScopeCodes.Count > 0)
        {
            throw new ValidationException($"权限码不属于当前租户应用菜单: {string.Join(", ", invalidScopeCodes)}");
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            var existingMappings = await databaseAccessor.GetCurrentDb().Queryable<SystemRolePermissionEntity>()
                .Where(item => item.RoleId == role.Id && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            var updatedTime = DateTime.UtcNow;

            if (existingMappings.Count > 0)
            {
                foreach (var mapping in existingMappings)
                {
                    mapping.IsDeleted = true;
                    mapping.DeletedTime = updatedTime;
                    mapping.UpdatedTime = updatedTime;
                }

                await databaseAccessor.GetCurrentDb().Updateable(existingMappings)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }

            if (normalizedCodes.Count == 0)
            {
                return;
            }

            var permissions = catalog
                .Where(item => normalizedCodes.Contains(item.PermissionCode, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var mappings = permissions.Select(permission => new SystemRolePermissionEntity
            {
                RoleId = role.Id,
                PermissionCodeId = permission.Id
            }).ToArray();

            await databaseAccessor.GetCurrentDb().Insertable(mappings).ExecuteCommandAsync(cancellationToken);
        }, cancellationToken);
    }

    private async Task<SystemRoleEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var entity = (await databaseAccessor.GetCurrentDb().Queryable<SystemRoleEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return entity ?? throw new NotFoundException("角色不存在", ErrorCodes.RoleNotFound);
    }

    private static RoleListItemResponse Map(SystemRoleEntity role, int userCount, int permissionCount)
    {
        return new RoleListItemResponse(
            role.Id,
            role.TenantId,
            role.AppCode,
            role.RoleName,
            role.RoleCode,
            role.DataScope,
            role.IsEnabled,
            userCount,
            permissionCount,
            role.Remark);
    }

    private async Task EnsureUniqueCodeAsync(string roleCode, string? currentId, WorkspaceScope scope, CancellationToken cancellationToken)
    {
        var normalizedCode = roleCode.Trim();
        var exists = await databaseAccessor.GetCurrentDb().Queryable<SystemRoleEntity>()
            .Where(item =>
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode &&
                item.RoleCode == normalizedCode &&
                item.Id != (currentId ?? string.Empty) &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new ValidationException("角色编码已存在");
        }
    }

    private async Task<IReadOnlyList<string>> GetPermissionCodesByRoleIdAsync(string roleId, CancellationToken cancellationToken)
    {
        var role = await GetRequiredAsync(roleId, cancellationToken);
        var permissionCodeIds = await databaseAccessor.GetCurrentDb().Queryable<SystemRolePermissionEntity>()
            .Where(item => item.RoleId == role.Id && !item.IsDeleted)
            .Select(item => item.PermissionCodeId)
            .ToListAsync(cancellationToken);

        if (permissionCodeIds.Count == 0)
        {
            return [];
        }

        return await databaseAccessor.GetCurrentDb().Queryable<SystemPermissionCodeEntity>()
            .Where(item => permissionCodeIds.Contains(item.Id) && !item.IsDeleted && item.IsEnabled)
            .Select(item => item.PermissionCode)
            .ToListAsync(cancellationToken);
    }

    private static List<string> NormalizeIds(IReadOnlyList<string> ids)
    {
        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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

    private sealed record WorkspaceScope(string TenantId, string AppCode);

    private static ISugarQueryable<SystemRoleEntity> ApplyDefaultSort(ISugarQueryable<SystemRoleEntity> query) =>
        query.OrderBy(item => item.CreatedTime, OrderByType.Desc);
}

