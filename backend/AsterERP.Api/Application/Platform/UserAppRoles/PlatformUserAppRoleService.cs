using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Platform;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.Platform.UserAppRoles;

public sealed class PlatformUserAppRoleService(ISqlSugarClient db, PlatformAccessGuard accessGuard) : IPlatformUserAppRoleService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemUserAppRoleEntity, SystemUserEntity, SystemTenantEntity, SystemApplicationEntity, SystemRoleEntity>, GridFilter, ISugarQueryable<SystemUserAppRoleEntity, SystemUserEntity, SystemTenantEntity, SystemApplicationEntity, SystemRoleEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemUserAppRoleEntity, SystemUserEntity, SystemTenantEntity, SystemApplicationEntity, SystemRoleEntity>, GridFilter, ISugarQueryable<SystemUserAppRoleEntity, SystemUserEntity, SystemTenantEntity, SystemApplicationEntity, SystemRoleEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["appCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (mapping, user, tenant, app, role) => mapping.AppCode),
            ["appName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (mapping, user, tenant, app, role) => app.AppName),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, (mapping, user, tenant, app, role) => mapping.CreatedTime),
            ["displayName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (mapping, user, tenant, app, role) => user.DisplayName),
            ["isDefault"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, (mapping, user, tenant, app, role) => mapping.IsDefault),
            ["remark"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (mapping, user, tenant, app, role) => mapping.Remark),
            ["roleName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (mapping, user, tenant, app, role) => role.RoleName),
            ["tenantName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (mapping, user, tenant, app, role) => tenant.TenantName),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, (mapping, user, tenant, app, role) => mapping.UpdatedTime),
            ["userName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (mapping, user, tenant, app, role) => user.UserName)
        };

    public async Task<GridPageResult<UserAppRoleResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var keyword = NormalizeOptional(gridQuery.Keyword);
        var tenantId = NormalizeOptional(gridQuery.TenantId);
        var appCode = NormalizeOptional(gridQuery.AppCode)?.ToUpperInvariant();
        var userId = NormalizeOptional(gridQuery.UserId);

        var query = db.Queryable<SystemUserAppRoleEntity, SystemUserEntity, SystemTenantEntity, SystemApplicationEntity, SystemRoleEntity>(
                (mapping, user, tenant, app, role) => mapping.UserId == user.Id && mapping.TenantId == tenant.Id && mapping.AppCode == app.AppCode && mapping.RoleId == role.Id)
            .Where((mapping, user, tenant, app, role) => !mapping.IsDeleted && !user.IsDeleted && !tenant.IsDeleted && !app.IsDeleted && !role.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where((mapping, user, tenant, app, role) =>
                user.UserName.Contains(keyword) ||
                user.DisplayName.Contains(keyword) ||
                tenant.TenantName.Contains(keyword) ||
                app.AppName.Contains(keyword) ||
                role.RoleName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            query = query.Where((mapping, user, tenant, app, role) => mapping.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(appCode))
        {
            query = query.Where((mapping, user, tenant, app, role) => mapping.AppCode == appCode);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where((mapping, user, tenant, app, role) => mapping.UserId == userId);
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, Filterers);

        var total = new RefAsync<int>();
        var items = await GridSortApplier
            .Apply(
                query,
                gridQuery.Sorts,
                (nextQuery, field, order) => field switch
                {
                    "appCode" => nextQuery.OrderBy((mapping, user, tenant, app, role) => mapping.AppCode, order),
                    "appName" => nextQuery.OrderBy((mapping, user, tenant, app, role) => app.AppName, order),
                    "createdTime" => nextQuery.OrderBy((mapping, user, tenant, app, role) => mapping.CreatedTime, order),
                    "displayName" => nextQuery.OrderBy((mapping, user, tenant, app, role) => user.DisplayName, order),
                    "isDefault" => nextQuery.OrderBy((mapping, user, tenant, app, role) => mapping.IsDefault, order),
                    "remark" => nextQuery.OrderBy((mapping, user, tenant, app, role) => mapping.Remark, order),
                    "roleName" => nextQuery.OrderBy((mapping, user, tenant, app, role) => role.RoleName, order),
                    "tenantName" => nextQuery.OrderBy((mapping, user, tenant, app, role) => tenant.TenantName, order),
                    "updatedTime" => nextQuery.OrderBy((mapping, user, tenant, app, role) => mapping.UpdatedTime, order),
                    "userName" => nextQuery.OrderBy((mapping, user, tenant, app, role) => user.UserName, order),
                    _ => null
                },
                nextQuery => nextQuery.OrderBy((mapping, user, tenant, app, role) => mapping.CreatedTime, OrderByType.Desc))
            .Select((mapping, user, tenant, app, role) => new UserAppRoleResponse(
                mapping.Id,
                mapping.UserId,
                user.UserName,
                user.DisplayName,
                mapping.TenantId,
                tenant.TenantName,
                mapping.AppCode,
                app.AppName,
                mapping.RoleId,
                role.RoleName,
                mapping.IsDefault,
                mapping.Remark))
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, total);

        return new GridPageResult<UserAppRoleResponse> { Total = total.Value, Items = items };
    }

    public async Task<UserAppRoleResponse> CreateAsync(UserAppRoleUpsertRequest request, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        await EnsureReferencesAsync(request, null, cancellationToken);
        await EnsureUniqueAsync(request, null, cancellationToken);

        var role = await GetRoleAsync(request.RoleId, request.TenantId, request.AppCode, cancellationToken);
        var entity = new SystemUserAppRoleEntity
        {
            UserId = request.UserId.Trim(),
            TenantId = request.TenantId.Trim(),
            AppCode = request.AppCode.Trim().ToUpperInvariant(),
            RoleId = role.Id,
            IsDefault = request.IsDefault,
            Remark = NormalizeOptional(request.Remark)
        };

        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapAsync(entity.Id, cancellationToken);
    }

    public async Task<UserAppRoleResponse> UpdateAsync(string id, UserAppRoleUpsertRequest request, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var entity = await GetRequiredAsync(id, cancellationToken);
        await EnsureReferencesAsync(request, id, cancellationToken);
        await EnsureUniqueAsync(request, id, cancellationToken);

        var role = await GetRoleAsync(request.RoleId, request.TenantId, request.AppCode, cancellationToken);
        entity.UserId = request.UserId.Trim();
        entity.TenantId = request.TenantId.Trim();
        entity.AppCode = request.AppCode.Trim().ToUpperInvariant();
        entity.RoleId = role.Id;
        entity.IsDefault = request.IsDefault;
        entity.Remark = NormalizeOptional(request.Remark);
        entity.UpdatedTime = DateTime.UtcNow;

        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var entity = await GetRequiredAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedTime = entity.DeletedTime;
        await db.Updateable(entity).UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
    }

    private async Task EnsureReferencesAsync(UserAppRoleUpsertRequest request, string? currentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.TenantId) ||
            string.IsNullOrWhiteSpace(request.AppCode) ||
            string.IsNullOrWhiteSpace(request.RoleId))
        {
            throw new ValidationException("用户、租户、应用和角色不能为空");
        }

        var normalizedAppCode = request.AppCode.Trim().ToUpperInvariant();
        var membershipExists = await db.Queryable<SystemUserTenantMembershipEntity>()
            .Where(item => item.UserId == request.UserId.Trim() && item.TenantId == request.TenantId.Trim() && !item.IsDeleted && item.Status == "Enabled")
            .AnyAsync(cancellationToken);
        if (!membershipExists)
        {
            throw new ValidationException("用户未加入该租户或关系已停用");
        }

        var tenantAppExists = await db.Queryable<SystemTenantAppEntity>()
            .Where(item => item.TenantId == request.TenantId.Trim() && item.AppCode == normalizedAppCode && !item.IsDeleted && item.Status == "Enabled")
            .AnyAsync(cancellationToken);
        if (!tenantAppExists)
        {
            throw new ValidationException("租户应用未安装或已停用");
        }

        _ = await GetRoleAsync(request.RoleId, request.TenantId, normalizedAppCode, cancellationToken);

        if (request.IsDefault)
        {
            var hasDefault = await db.Queryable<SystemUserAppRoleEntity>()
                .Where(item =>
                    item.UserId == request.UserId.Trim() &&
                    item.TenantId == request.TenantId.Trim() &&
                    item.AppCode == normalizedAppCode &&
                    item.Id != (currentId ?? string.Empty) &&
                    item.IsDefault &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (hasDefault)
            {
                throw new ValidationException("该用户在当前租户应用下已有默认角色");
            }
        }
    }

    private async Task<SystemRoleEntity> GetRoleAsync(string roleId, string tenantId, string appCode, CancellationToken cancellationToken)
    {
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        return (await db.Queryable<SystemRoleEntity>()
            .Where(item =>
                item.Id == roleId.Trim() &&
                item.TenantId == tenantId.Trim() &&
                item.AppCode == normalizedAppCode &&
                !item.IsDeleted &&
                item.IsEnabled)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new ValidationException("角色不存在、已停用或不属于当前租户应用");
    }

    private async Task EnsureUniqueAsync(UserAppRoleUpsertRequest request, string? currentId, CancellationToken cancellationToken)
    {
        var normalizedAppCode = request.AppCode.Trim().ToUpperInvariant();
        var exists = await db.Queryable<SystemUserAppRoleEntity>()
            .Where(item =>
                item.UserId == request.UserId.Trim() &&
                item.TenantId == request.TenantId.Trim() &&
                item.AppCode == normalizedAppCode &&
                item.RoleId == request.RoleId.Trim() &&
                item.Id != (currentId ?? string.Empty) &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException("用户应用角色关系已存在");
        }
    }

    private async Task<SystemUserAppRoleEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemUserAppRoleEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("用户应用角色关系不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<UserAppRoleResponse> MapAsync(string id, CancellationToken cancellationToken)
    {
        var mapping = await GetRequiredAsync(id, cancellationToken);
        var user = (await db.Queryable<SystemUserEntity>()
            .Where(item => item.Id == mapping.UserId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("用户不存在", ErrorCodes.PlatformResourceNotFound);
        var tenant = (await db.Queryable<SystemTenantEntity>()
            .Where(item => item.Id == mapping.TenantId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("租户不存在", ErrorCodes.PlatformResourceNotFound);
        var app = (await db.Queryable<SystemApplicationEntity>()
            .Where(item => item.AppCode == mapping.AppCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("应用不存在", ErrorCodes.PlatformResourceNotFound);
        var role = (await db.Queryable<SystemRoleEntity>()
            .Where(item => item.Id == mapping.RoleId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("角色不存在", ErrorCodes.PlatformResourceNotFound);

        return new UserAppRoleResponse(
            mapping.Id,
            mapping.UserId,
            user.UserName,
            user.DisplayName,
            mapping.TenantId,
            tenant.TenantName,
            mapping.AppCode,
            app.AppName,
            mapping.RoleId,
            role.RoleName,
            mapping.IsDefault,
            mapping.Remark);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
