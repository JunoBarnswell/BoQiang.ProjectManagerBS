using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Platform;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.Platform.UserTenants;

public sealed class PlatformUserTenantService(ISqlSugarClient db, PlatformAccessGuard accessGuard) : IPlatformUserTenantService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemUserTenantMembershipEntity, SystemUserEntity, SystemTenantEntity, SystemDepartmentEntity, SystemPositionEntity>, GridFilter, ISugarQueryable<SystemUserTenantMembershipEntity, SystemUserEntity, SystemTenantEntity, SystemDepartmentEntity, SystemPositionEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemUserTenantMembershipEntity, SystemUserEntity, SystemTenantEntity, SystemDepartmentEntity, SystemPositionEntity>, GridFilter, ISugarQueryable<SystemUserTenantMembershipEntity, SystemUserEntity, SystemTenantEntity, SystemDepartmentEntity, SystemPositionEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, (membership, user, tenant, dept, position) => membership.CreatedTime),
            ["deptName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (membership, user, tenant, dept, position) => dept.DeptName),
            ["displayName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (membership, user, tenant, dept, position) => user.DisplayName),
            ["isDefault"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, (membership, user, tenant, dept, position) => membership.IsDefault),
            ["isTenantAdmin"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, (membership, user, tenant, dept, position) => membership.IsTenantAdmin),
            ["positionName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (membership, user, tenant, dept, position) => position.PositionName),
            ["status"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (membership, user, tenant, dept, position) => membership.Status),
            ["tenantName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (membership, user, tenant, dept, position) => tenant.TenantName),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, (membership, user, tenant, dept, position) => membership.UpdatedTime),
            ["userName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (membership, user, tenant, dept, position) => user.UserName)
        };

    public async Task<GridPageResult<UserTenantMembershipResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var keyword = NormalizeOptional(gridQuery.Keyword);
        var tenantId = NormalizeOptional(gridQuery.TenantId);
        var userId = NormalizeOptional(gridQuery.UserId);
        var status = NormalizeOptional(gridQuery.Status);

        var query = db.Queryable<SystemUserTenantMembershipEntity, SystemUserEntity, SystemTenantEntity>(
                (membership, user, tenant) => membership.UserId == user.Id && membership.TenantId == tenant.Id)
            .LeftJoin<SystemDepartmentEntity>((membership, user, tenant, dept) => membership.DeptId == dept.Id)
            .LeftJoin<SystemPositionEntity>((membership, user, tenant, dept, position) => membership.PositionId == position.Id)
            .Where((membership, user, tenant, dept, position) => !membership.IsDeleted && !user.IsDeleted && !tenant.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where((membership, user, tenant, dept, position) =>
                user.UserName.Contains(keyword) ||
                user.DisplayName.Contains(keyword) ||
                tenant.TenantName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            query = query.Where((membership, user, tenant, dept, position) => membership.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where((membership, user, tenant, dept, position) => membership.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where((membership, user, tenant, dept, position) => membership.Status == status);
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, Filterers);

        var total = new RefAsync<int>();
        var items = await GridSortApplier
            .Apply(
                query,
                gridQuery.Sorts,
                (nextQuery, field, order) => field switch
                {
                    "createdTime" => nextQuery.OrderBy((membership, user, tenant, dept, position) => membership.CreatedTime, order),
                    "deptName" => nextQuery.OrderBy((membership, user, tenant, dept, position) => dept.DeptName, order),
                    "displayName" => nextQuery.OrderBy((membership, user, tenant, dept, position) => user.DisplayName, order),
                    "isDefault" => nextQuery.OrderBy((membership, user, tenant, dept, position) => membership.IsDefault, order),
                    "isTenantAdmin" => nextQuery.OrderBy((membership, user, tenant, dept, position) => membership.IsTenantAdmin, order),
                    "positionName" => nextQuery.OrderBy((membership, user, tenant, dept, position) => position.PositionName, order),
                    "status" => nextQuery.OrderBy((membership, user, tenant, dept, position) => membership.Status, order),
                    "tenantName" => nextQuery.OrderBy((membership, user, tenant, dept, position) => tenant.TenantName, order),
                    "updatedTime" => nextQuery.OrderBy((membership, user, tenant, dept, position) => membership.UpdatedTime, order),
                    "userName" => nextQuery.OrderBy((membership, user, tenant, dept, position) => user.UserName, order),
                    _ => null
                },
                nextQuery => nextQuery.OrderBy((membership, user, tenant, dept, position) => membership.CreatedTime, OrderByType.Desc))
            .Select((membership, user, tenant, dept, position) => new UserTenantMembershipResponse(
                membership.Id,
                membership.UserId,
                user.UserName,
                user.DisplayName,
                membership.TenantId,
                tenant.TenantName,
                membership.DeptId,
                dept.DeptName,
                membership.PositionId,
                position.PositionName,
                membership.IsTenantAdmin,
                membership.IsDefault,
                membership.Status,
                membership.Remark))
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, total);

        return new GridPageResult<UserTenantMembershipResponse> { Total = total.Value, Items = items };
    }

    public async Task<UserTenantMembershipResponse> CreateAsync(UserTenantMembershipUpsertRequest request, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        await EnsureReferencesAsync(request, null, cancellationToken);
        await EnsureUniqueAsync(request.UserId, request.TenantId, null, cancellationToken);

        var entity = new SystemUserTenantMembershipEntity
        {
            UserId = request.UserId.Trim(),
            TenantId = request.TenantId.Trim(),
            DeptId = NormalizeOptional(request.DeptId),
            PositionId = NormalizeOptional(request.PositionId),
            IsTenantAdmin = request.IsTenantAdmin,
            IsDefault = request.IsDefault,
            Status = NormalizeStatus(request.Status),
            Remark = NormalizeOptional(request.Remark)
        };

        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapAsync(entity.Id, cancellationToken);
    }

    public async Task<UserTenantMembershipResponse> UpdateAsync(string id, UserTenantMembershipUpsertRequest request, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var entity = await GetRequiredAsync(id, cancellationToken);
        await EnsureReferencesAsync(request, id, cancellationToken);
        await EnsureUniqueAsync(request.UserId, request.TenantId, id, cancellationToken);
        await EnsureTenantAdminRetainedAsync(
            new[] { entity },
            new Dictionary<string, (string TenantId, bool IsTenantAdmin, string Status)>(StringComparer.OrdinalIgnoreCase)
            {
                [entity.Id] = (request.TenantId.Trim(), request.IsTenantAdmin, NormalizeStatus(request.Status))
            },
            cancellationToken);

        entity.UserId = request.UserId.Trim();
        entity.TenantId = request.TenantId.Trim();
        entity.DeptId = NormalizeOptional(request.DeptId);
        entity.PositionId = NormalizeOptional(request.PositionId);
        entity.IsTenantAdmin = request.IsTenantAdmin;
        entity.IsDefault = request.IsDefault;
        entity.Status = NormalizeStatus(request.Status);
        entity.Remark = NormalizeOptional(request.Remark);
        entity.UpdatedTime = DateTime.UtcNow;

        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var entity = await GetRequiredAsync(id, cancellationToken);
        await EnsureTenantAdminRetainedAsync(
            new[] { entity },
            new Dictionary<string, (string TenantId, bool IsTenantAdmin, string Status)>(StringComparer.OrdinalIgnoreCase)
            {
                [entity.Id] = (entity.TenantId, entity.IsTenantAdmin, "Deleted")
            },
            cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedTime = entity.DeletedTime;
        await db.Updateable(entity).UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
    }

    public async Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var normalizedIds = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (normalizedIds.Count == 0)
        {
            return;
        }

        var entities = await db.Queryable<SystemUserTenantMembershipEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (entities.Count != normalizedIds.Count)
        {
            throw new NotFoundException("用户租户关系不存在", ErrorCodes.PlatformResourceNotFound);
        }

        var normalizedStatus = NormalizeStatus(status);
        await EnsureTenantAdminRetainedAsync(
            entities,
            entities.ToDictionary(
                entity => entity.Id,
                entity => (entity.TenantId, entity.IsTenantAdmin, normalizedStatus),
                StringComparer.OrdinalIgnoreCase),
            cancellationToken);
        var updatedTime = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            entity.Status = normalizedStatus;
            entity.UpdatedTime = updatedTime;
        }

        await db.Updateable(entities).UpdateColumns(item => new { item.Status, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
    }

    private async Task EnsureReferencesAsync(UserTenantMembershipUpsertRequest request, string? currentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.TenantId))
        {
            throw new ValidationException("用户和租户不能为空");
        }

        var userExists = await db.Queryable<SystemUserEntity>().Where(item => item.Id == request.UserId.Trim() && !item.IsDeleted && item.Status == "Enabled").AnyAsync(cancellationToken);
        if (!userExists)
        {
            throw new ValidationException("用户不存在或已停用");
        }

        var tenantExists = await db.Queryable<SystemTenantEntity>().Where(item => item.Id == request.TenantId.Trim() && !item.IsDeleted && item.Status == "Enabled").AnyAsync(cancellationToken);
        if (!tenantExists)
        {
            throw new ValidationException("租户不存在或已停用");
        }

        var deptId = NormalizeOptional(request.DeptId);
        if (!string.IsNullOrWhiteSpace(deptId))
        {
            var deptExists = await db.Queryable<SystemDepartmentEntity>().Where(item => item.Id == deptId && !item.IsDeleted && item.Status == "Enabled").AnyAsync(cancellationToken);
            if (!deptExists)
            {
                throw new ValidationException("部门不存在或已停用");
            }
        }

        var positionId = NormalizeOptional(request.PositionId);
        if (!string.IsNullOrWhiteSpace(positionId))
        {
            var position = (await db.Queryable<SystemPositionEntity>()
                .Where(item => item.Id == positionId && !item.IsDeleted && item.Status == "Enabled")
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();
            if (position is null)
            {
                throw new ValidationException("岗位不存在或已停用");
            }

            if (!string.IsNullOrWhiteSpace(deptId) && !string.Equals(position.DeptId, deptId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("岗位不属于所选部门");
            }
        }

        if (request.IsDefault)
        {
            var hasDefault = await db.Queryable<SystemUserTenantMembershipEntity>()
                .Where(item => item.UserId == request.UserId.Trim() && item.Id != (currentId ?? string.Empty) && item.IsDefault && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (hasDefault)
            {
                throw new ValidationException("该用户已有默认租户");
            }
        }
    }

    private async Task EnsureTenantAdminRetainedAsync(
        IReadOnlyList<SystemUserTenantMembershipEntity> impactedEntities,
        IReadOnlyDictionary<string, (string TenantId, bool IsTenantAdmin, string Status)> nextStates,
        CancellationToken cancellationToken)
    {
        var impactedTenantIds = impactedEntities
            .Where(entity => entity.IsTenantAdmin && entity.Status == "Enabled")
            .Select(entity => entity.TenantId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (impactedTenantIds.Count == 0)
        {
            return;
        }

        var tenantAdmins = await db.Queryable<SystemUserTenantMembershipEntity>()
            .Where(item => impactedTenantIds.Contains(item.TenantId) && item.IsTenantAdmin && item.Status == "Enabled" && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in impactedTenantIds)
        {
            var retainedCount = tenantAdmins.Count(admin =>
            {
                if (!string.Equals(admin.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!nextStates.TryGetValue(admin.Id, out var nextState))
                {
                    return true;
                }

                return string.Equals(nextState.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) &&
                       nextState.IsTenantAdmin &&
                       nextState.Status == "Enabled";
            });

            if (retainedCount == 0)
            {
                throw new ValidationException("每个租户至少需要保留一个启用状态的租户超级管理员");
            }
        }
    }

    private async Task EnsureUniqueAsync(string userId, string tenantId, string? currentId, CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<SystemUserTenantMembershipEntity>()
            .Where(item => item.UserId == userId.Trim() && item.TenantId == tenantId.Trim() && item.Id != (currentId ?? string.Empty) && !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException("用户租户关系已存在");
        }
    }

    private async Task<SystemUserTenantMembershipEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemUserTenantMembershipEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("用户租户关系不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<UserTenantMembershipResponse> MapAsync(string id, CancellationToken cancellationToken)
    {
        var membership = await GetRequiredAsync(id, cancellationToken);
        var user = (await db.Queryable<SystemUserEntity>()
            .Where(item => item.Id == membership.UserId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("用户不存在", ErrorCodes.PlatformResourceNotFound);
        var tenant = (await db.Queryable<SystemTenantEntity>()
            .Where(item => item.Id == membership.TenantId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("租户不存在", ErrorCodes.PlatformResourceNotFound);
        var dept = string.IsNullOrWhiteSpace(membership.DeptId)
            ? null
            : (await db.Queryable<SystemDepartmentEntity>()
                .Where(item => item.Id == membership.DeptId && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();
        var position = string.IsNullOrWhiteSpace(membership.PositionId)
            ? null
            : (await db.Queryable<SystemPositionEntity>()
                .Where(item => item.Id == membership.PositionId && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();

        return new UserTenantMembershipResponse(
            membership.Id,
            membership.UserId,
            user.UserName,
            user.DisplayName,
            membership.TenantId,
            tenant.TenantName,
            membership.DeptId,
            dept?.DeptName,
            membership.PositionId,
            position?.PositionName,
            membership.IsTenantAdmin,
            membership.IsDefault,
            membership.Status,
            membership.Remark);
    }

    private static string NormalizeStatus(string status)
    {
        return status.Trim().Equals("Disabled", StringComparison.OrdinalIgnoreCase) ? "Disabled" : "Enabled";
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
