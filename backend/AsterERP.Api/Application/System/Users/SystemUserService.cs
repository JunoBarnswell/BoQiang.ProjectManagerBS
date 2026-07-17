using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.Users;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Domain.System.Users;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.System.Users;

public sealed class SystemUserService(
    ICurrentUser currentUser,
    IWorkspaceDatabaseAccessor databaseAccessor,
    IUnitOfWork unitOfWork,
    IAuthSessionService authSessionService,
    IPasswordHashService passwordHashService) : ISystemUserService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemUserEntity>, OrderByType, ISugarQueryable<SystemUserEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemUserEntity>, OrderByType, ISugarQueryable<SystemUserEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["displayName"] = (query, order) => query.OrderBy(item => item.DisplayName, order),
            ["email"] = (query, order) => query.OrderBy(item => item.Email, order),
            ["phoneNumber"] = (query, order) => query.OrderBy(item => item.PhoneNumber, order),
            ["status"] = (query, order) => query.OrderBy(item => item.Status, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order),
            ["userName"] = (query, order) => query.OrderBy(item => item.UserName, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemUserEntity>, GridFilter, ISugarQueryable<SystemUserEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemUserEntity>, GridFilter, ISugarQueryable<SystemUserEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["displayName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.DisplayName),
            ["email"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Email),
            ["phoneNumber"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.PhoneNumber),
            ["status"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Status),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime),
            ["userName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.UserName)
        };

    public async Task<GridPageResult<UserListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        var keyword = gridQuery.Keyword?.Trim();
        var query = databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>().Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                item.UserName.Contains(keyword) ||
                item.DisplayName.Contains(keyword) ||
                (item.PhoneNumber != null && item.PhoneNumber.Contains(keyword)) ||
                (item.Email != null && item.Email.Contains(keyword)));
        }

        var status = NormalizeOptional(gridQuery.Status);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }

        var deptId = NormalizeOptional(gridQuery.DeptId);
        if (!string.IsNullOrWhiteSpace(deptId))
        {
            var deptIds = gridQuery.IncludeDescendants
                ? await ResolveDepartmentAndChildrenAsync(deptId, cancellationToken)
                : [deptId];
            var scopedUserIds = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
                .Where(item => deptIds.Contains(item.DeptId) && !item.IsDeleted && item.Status == "Enabled")
                .Select(item => item.UserId)
                .ToListAsync(cancellationToken);
            if (scopedUserIds.Count == 0)
            {
                query = query.Where(item => deptIds.Contains(item.DeptId!));
            }
            else
            {
                var distinctUserIds = scopedUserIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                query = query.Where(item => distinctUserIds.Contains(item.Id) || deptIds.Contains(item.DeptId!));
            }
        }

        var positionId = NormalizeOptional(gridQuery.PositionId);
        if (!string.IsNullOrWhiteSpace(positionId))
        {
            var scopedUserIds = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
                .Where(item => item.PositionId == positionId && !item.IsDeleted && item.Status == "Enabled")
                .Select(item => item.UserId)
                .ToListAsync(cancellationToken);
            if (scopedUserIds.Count == 0)
            {
                query = query.Where(item => item.PositionId == positionId);
            }
            else
            {
                var distinctUserIds = scopedUserIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                query = query.Where(item => distinctUserIds.Contains(item.Id) || item.PositionId == positionId);
            }
        }

        var roleId = NormalizeOptional(gridQuery.RoleId);
        if (!string.IsNullOrWhiteSpace(roleId))
        {
            var scopedUserIds = await databaseAccessor.GetCurrentDb().Queryable<SystemUserRoleEntity>()
                .Where(item => item.RoleId == roleId && !item.IsDeleted)
                .Select(item => item.UserId)
                .ToListAsync(cancellationToken);
            query = scopedUserIds.Count == 0
                ? query.Where(item => item.Id == "__none__")
                : query.Where(item => scopedUserIds.Contains(item.Id));
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, Filterers);

        var totalCount = new RefAsync<int>();
        var users = await GridSortApplier
            .Apply(query, gridQuery.Sorts, Sorters, ApplyDefaultSort)
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, totalCount);

        if (users.Count == 0)
        {
            return new GridPageResult<UserListItemResponse> { Total = totalCount.Value, Items = [] };
        }

        var userIds = users.Select(item => item.Id).ToList();
        var userRoles = await databaseAccessor.GetCurrentDb().Queryable<SystemUserRoleEntity>()
            .Where(item => userIds.Contains(item.UserId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var roleIds = userRoles.Select(item => item.RoleId).Distinct().ToList();
        var roles = roleIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemRoleEntity>()
                .Where(item => roleIds.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var rolesByUser = userRoles
            .GroupBy(item => item.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(mapping => mapping.RoleId).Distinct().ToList());
        var roleNamesByUser = rolesByUser.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .Select(roleIdValue => roles.FirstOrDefault(role => role.Id == roleIdValue)?.RoleName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList());

        var employmentsByUser = await LoadEmploymentResponsesAsync(userIds, cancellationToken);
        var departmentIds = users
            .Select(item => ResolvePrimaryEmployment(employmentsByUser.GetValueOrDefault(item.Id))?.DeptId ?? item.DeptId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();
        var departments = departmentIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
                .Where(item => departmentIds.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var departmentNames = departments.ToDictionary(item => item.Id, item => item.DeptName);

        var positionIds = users
            .Select(item => ResolvePrimaryEmployment(employmentsByUser.GetValueOrDefault(item.Id))?.PositionId ?? item.PositionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();
        var positions = positionIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
                .Where(item => positionIds.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var positionNames = positions.ToDictionary(item => item.Id, item => item.PositionName);

        return new GridPageResult<UserListItemResponse>
        {
            Total = totalCount.Value,
            Items = users.Select(user =>
            {
                var userRoleIds = rolesByUser.TryGetValue(user.Id, out var mappedRoleIds) ? mappedRoleIds : [];
                var userRoleNames = roleNamesByUser.TryGetValue(user.Id, out var names) ? names : [];
                var employments = employmentsByUser.TryGetValue(user.Id, out var mappedEmployments) ? mappedEmployments : [];
                var primaryEmployment = ResolvePrimaryEmployment(employments);
                var primaryDeptId = primaryEmployment?.DeptId ?? user.DeptId;
                var primaryPositionId = primaryEmployment?.PositionId ?? user.PositionId;

                return new UserListItemResponse(
                    user.Id,
                    user.UserName,
                    user.DisplayName,
                    user.PhoneNumber,
                    user.Email,
                    primaryDeptId,
                    primaryDeptId is not null && departmentNames.TryGetValue(primaryDeptId, out var deptName) ? deptName : null,
                    primaryPositionId,
                    primaryPositionId is not null && positionNames.TryGetValue(primaryPositionId, out var positionName) ? positionName : null,
                    user.IsAdmin,
                    user.Status,
                    ResolveDataScope(user, roles.Where(role => userRoleIds.Contains(role.Id)).ToList()),
                    userRoleIds,
                    userRoleNames,
                    user.Remark,
                    employments,
                    primaryEmployment?.Id,
                    primaryDeptId,
                    primaryPositionId,
                    BuildEmploymentSummary(employments));
            }).ToList()
        };
    }

    public async Task<UserListItemResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        return await MapAsync(await GetRequiredAsync(id, cancellationToken), cancellationToken);
    }

    public async Task<UserListItemResponse> CreateAsync(UserUpsertRequest request, CancellationToken cancellationToken = default)
    {
        UserDomainPolicy.EnsureCreateRequest(request.UserName, request.DisplayName, request.Password, request.Status);
        await EnsureUniqueUserNameAsync(request.UserName, null, cancellationToken);
        await EnsureRolesExistAsync(request.RoleIds, cancellationToken);
        var employments = await BuildEmploymentEntitiesAsync(string.Empty, request, cancellationToken);
        var primaryEmployment = ResolvePrimaryEmployment(employments);

        var user = new SystemUserEntity
        {
            UserName = request.UserName.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHashService.HashPassword(request.Password.Trim()),
            PasswordResetRequired = false,
            PasswordFormatVersion = "v1",
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            Email = NormalizeOptional(request.Email),
            DeptId = primaryEmployment?.DeptId,
            PositionId = primaryEmployment?.PositionId,
            IsAdmin = request.IsAdmin,
            Status = NormalizeStatus(request.Status),
            Remark = NormalizeOptional(request.Remark)
        };

        await unitOfWork.ExecuteAsync(async () =>
        {
            await databaseAccessor.GetCurrentDb().Insertable(user).ExecuteCommandAsync(cancellationToken);
            foreach (var employment in employments)
            {
                employment.UserId = user.Id;
            }

            await InsertEmploymentsAsync(employments, cancellationToken);
            await UpdateRoleMappingsAsync(user.Id, request.RoleIds, cancellationToken);
        }, cancellationToken);

        return await MapAsync(user, cancellationToken);
    }

    public async Task<UserListItemResponse> UpdateAsync(string id, UserUpsertRequest request, CancellationToken cancellationToken = default)
    {
        UserDomainPolicy.EnsureUpdateRequest(request.UserName, request.DisplayName, request.Status);
        var user = await GetRequiredAsync(id, cancellationToken);
        await EnsureUniqueUserNameAsync(request.UserName, id, cancellationToken);
        await EnsureRolesExistAsync(request.RoleIds, cancellationToken);
        var employments = await BuildEmploymentEntitiesAsync(user.Id, request, cancellationToken);
        var primaryEmployment = ResolvePrimaryEmployment(employments);

        user.UserName = request.UserName.Trim();
        user.DisplayName = request.DisplayName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = passwordHashService.HashPassword(request.Password.Trim());
            user.PasswordResetRequired = false;
            user.PasswordFormatVersion = "v1";
        }

        user.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        user.Email = NormalizeOptional(request.Email);
        user.DeptId = primaryEmployment?.DeptId;
        user.PositionId = primaryEmployment?.PositionId;
        user.IsAdmin = request.IsAdmin;
        user.Status = NormalizeStatus(request.Status);
        user.Remark = NormalizeOptional(request.Remark);
        user.UpdatedTime = DateTime.UtcNow;

        await unitOfWork.ExecuteAsync(async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable(user).ExecuteCommandAsync(cancellationToken);
            await ReplaceEmploymentsAsync(user.Id, employments, cancellationToken);
            await UpdateRoleMappingsAsync(user.Id, request.RoleIds, cancellationToken);
        }, cancellationToken);

        return await MapAsync(user, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await DeleteUsersAsync([id], cancellationToken);
    }

    public async Task BatchDeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        await DeleteUsersAsync(ids, cancellationToken);
    }

    public async Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default)
    {
        var normalizedIds = NormalizeIds(ids);
        if (normalizedIds.Count == 0)
        {
            return;
        }

        if (normalizedIds.Contains(currentUser.GetAsterErpUserId(), StringComparer.OrdinalIgnoreCase) &&
            status.Trim().Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("不能停用当前登录用户", ErrorCodes.StateChangeNotAllowed);
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        if (users.Count != normalizedIds.Count)
        {
            throw new NotFoundException("用户不存在", ErrorCodes.UserNotFound);
        }

        var normalizedStatus = NormalizeStatus(status);
        var updatedTime = DateTime.UtcNow;
        foreach (var user in users)
        {
            user.Status = normalizedStatus;
            user.UpdatedTime = updatedTime;
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable(users)
                .UpdateColumns(item => new { item.Status, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);

            await databaseAccessor.GetCurrentDb().Updateable<SystemUserEmploymentEntity>()
                .SetColumns(item => item.Status == normalizedStatus)
                .SetColumns(item => item.UpdatedTime == updatedTime)
                .Where(item => normalizedIds.Contains(item.UserId) && !item.IsDeleted)
                .ExecuteCommandAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task UpdateRolesAsync(string id, UserRoleUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredAsync(id, cancellationToken);
        await EnsureRolesExistAsync(request.RoleIds, cancellationToken);
        await UpdateRoleMappingsAsync(user.Id, request.RoleIds, cancellationToken);
    }

    public async Task ResetPasswordAsync(string id, UserResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("密码不能为空");
        }

        var user = await GetRequiredAsync(id, cancellationToken);
        user.PasswordHash = passwordHashService.HashPassword(request.Password.Trim());
        user.PasswordResetRequired = false;
        user.PasswordFormatVersion = "v1";
        user.UpdatedTime = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Updateable(user).UpdateColumns(item => new { item.PasswordHash, item.PasswordResetRequired, item.PasswordFormatVersion, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<UserListItemResponse> MapAsync(SystemUserEntity user, CancellationToken cancellationToken)
    {
        var roleIds = await databaseAccessor.GetCurrentDb().Queryable<SystemUserRoleEntity>()
            .Where(item => item.UserId == user.Id && !item.IsDeleted)
            .Select(item => item.RoleId)
            .ToListAsync(cancellationToken);
        var roles = roleIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemRoleEntity>()
                .Where(item => roleIds.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var employments = await LoadEmploymentResponsesAsync(user.Id, cancellationToken);
        var primaryEmployment = ResolvePrimaryEmployment(employments);
        var primaryDeptId = primaryEmployment?.DeptId ?? user.DeptId;
        var primaryPositionId = primaryEmployment?.PositionId ?? user.PositionId;

        return new UserListItemResponse(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.PhoneNumber,
            user.Email,
            primaryDeptId,
            await ResolveDepartmentNameAsync(primaryDeptId, cancellationToken),
            primaryPositionId,
            await ResolvePositionNameAsync(primaryPositionId, cancellationToken),
            user.IsAdmin,
            user.Status,
            ResolveDataScope(user, roles),
            roleIds,
            roles.Select(item => item.RoleName).ToList(),
            user.Remark,
            employments,
            primaryEmployment?.Id,
            primaryDeptId,
            primaryPositionId,
            BuildEmploymentSummary(employments));
    }

    private async Task<SystemUserEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var entity = (await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return entity ?? throw new NotFoundException("用户不存在", ErrorCodes.UserNotFound);
    }

    private async Task EnsureUniqueUserNameAsync(string userName, string? currentId, CancellationToken cancellationToken)
    {
        var normalizedName = userName.Trim();
        var exists = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .ClearFilter()
            .Where(item => item.UserName == normalizedName && item.Id != (currentId ?? string.Empty) && !item.IsDeleted)
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new ValidationException("用户名已存在");
        }
    }

    private async Task EnsureRolesExistAsync(IReadOnlyList<string> roleIds, CancellationToken cancellationToken)
    {
        var normalizedIds = NormalizeIds(roleIds);
        if (normalizedIds.Count == 0)
        {
            return;
        }

        var existingIds = await databaseAccessor.GetCurrentDb().Queryable<SystemRoleEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var missingIds = normalizedIds.Where(roleId => !existingIds.Contains(roleId)).ToList();
        if (missingIds.Count > 0)
        {
            throw new ValidationException($"角色不存在: {string.Join(", ", missingIds)}");
        }
    }

    private async Task EnsureDepartmentExistsAsync(string? deptId, CancellationToken cancellationToken)
    {
        var normalizedDeptId = NormalizeOptional(deptId);
        if (normalizedDeptId is null)
        {
            return;
        }

        var exists = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => item.Id == normalizedDeptId && !item.IsDeleted && item.Status == "Enabled")
            .AnyAsync(cancellationToken);

        if (!exists)
        {
            throw new ValidationException("所属部门不存在或已停用");
        }
    }

    private async Task EnsurePositionExistsAsync(string? positionId, string? deptId, CancellationToken cancellationToken)
    {
        var normalizedPositionId = NormalizeOptional(positionId);
        if (normalizedPositionId is null)
        {
            return;
        }

        var position = (await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
            .Where(item => item.Id == normalizedPositionId && !item.IsDeleted && item.Status == "Enabled")
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (position is null)
        {
            throw new ValidationException("岗位不存在或已停用");
        }

        var normalizedDeptId = NormalizeOptional(deptId);
        if (!string.IsNullOrWhiteSpace(normalizedDeptId) &&
            !string.Equals(position.DeptId, normalizedDeptId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("岗位不属于所选部门");
        }
    }

    private async Task<List<string>> ResolveDepartmentAndChildrenAsync(string rootDeptId, CancellationToken cancellationToken)
    {
        var departments = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var result = new List<string> { rootDeptId };
        var queue = new Queue<string>();
        queue.Enqueue(rootDeptId);

        while (queue.Count > 0)
        {
            var currentDeptId = queue.Dequeue();
            var children = departments
                .Where(item => string.Equals(item.ParentId, currentDeptId, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Id)
                .ToList();

            foreach (var child in children)
            {
                if (result.Contains(child, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(child);
                queue.Enqueue(child);
            }
        }

        return result;
    }

    private async Task<string?> ResolveDepartmentNameAsync(string? deptId, CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(deptId)
            ? null
            : (await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
                .Where(item => item.Id == deptId && !item.IsDeleted)
                .Select(item => item.DeptName)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();
    }

    private async Task<string?> ResolvePositionNameAsync(string? positionId, CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(positionId)
            ? null
            : (await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
                .Where(item => item.Id == positionId && !item.IsDeleted)
                .Select(item => item.PositionName)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();
    }

    private async Task UpdateRoleMappingsAsync(string userId, IReadOnlyList<string> roleIds, CancellationToken cancellationToken)
    {
        var normalizedIds = NormalizeIds(roleIds);
        var existingMappings = await databaseAccessor.GetCurrentDb().Queryable<SystemUserRoleEntity>()
            .Where(item => item.UserId == userId && !item.IsDeleted)
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

        if (normalizedIds.Count == 0)
        {
            return;
        }

        var mappings = normalizedIds.Select(roleId => new SystemUserRoleEntity
        {
            UserId = userId,
            RoleId = roleId
        }).ToArray();

        await databaseAccessor.GetCurrentDb().Insertable(mappings).ExecuteCommandAsync(cancellationToken);
    }

    private async Task DeleteUsersAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var normalizedIds = NormalizeIds(ids);
        if (normalizedIds.Count == 0)
        {
            return;
        }

        if (normalizedIds.Contains(currentUser.GetAsterErpUserId(), StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("不能删除当前登录用户", ErrorCodes.StateChangeNotAllowed);
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        if (users.Count != normalizedIds.Count)
        {
            throw new NotFoundException("用户不存在", ErrorCodes.UserNotFound);
        }

        var userRoleMappings = await databaseAccessor.GetCurrentDb().Queryable<SystemUserRoleEntity>()
            .Where(item => normalizedIds.Contains(item.UserId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var employments = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
            .Where(item => normalizedIds.Contains(item.UserId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var deletedTime = DateTime.UtcNow;

        foreach (var user in users)
        {
            user.IsDeleted = true;
            user.DeletedTime = deletedTime;
            user.UpdatedTime = deletedTime;
        }

        foreach (var mapping in userRoleMappings)
        {
            mapping.IsDeleted = true;
            mapping.DeletedTime = deletedTime;
            mapping.UpdatedTime = deletedTime;
        }

        foreach (var employment in employments)
        {
            employment.IsDeleted = true;
            employment.DeletedTime = deletedTime;
            employment.UpdatedTime = deletedTime;
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            await authSessionService.RevokeSessionsByUserIdsAsync(normalizedIds, cancellationToken);

            await databaseAccessor.GetCurrentDb().Updateable(users)
                .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);

            if (userRoleMappings.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(userRoleMappings)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }

            if (employments.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(employments)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }
        }, cancellationToken);
    }

    private async Task<Dictionary<string, IReadOnlyList<UserEmploymentResponse>>> LoadEmploymentResponsesAsync(
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<UserEmploymentResponse>>(StringComparer.OrdinalIgnoreCase);
        }

        var employments = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
            .Where(item => userIds.Contains(item.UserId) && !item.IsDeleted)
            .OrderBy(item => item.UserId)
            .OrderBy(item => item.IsPrimary, OrderByType.Desc)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        return await MapEmploymentResponsesAsync(employments, cancellationToken);
    }

    private async Task<IReadOnlyList<UserEmploymentResponse>> LoadEmploymentResponsesAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var grouped = await LoadEmploymentResponsesAsync([userId], cancellationToken);
        return grouped.TryGetValue(userId, out var employments) ? employments : [];
    }

    private async Task<Dictionary<string, IReadOnlyList<UserEmploymentResponse>>> MapEmploymentResponsesAsync(
        IReadOnlyList<SystemUserEmploymentEntity> employments,
        CancellationToken cancellationToken)
    {
        if (employments.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<UserEmploymentResponse>>(StringComparer.OrdinalIgnoreCase);
        }

        var deptIds = employments.Select(item => item.DeptId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var positionIds = employments.Select(item => item.PositionId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var departments = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => deptIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var positions = await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
            .Where(item => positionIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var deptNames = departments.ToDictionary(item => item.Id, item => item.DeptName, StringComparer.OrdinalIgnoreCase);
        var positionNames = positions.ToDictionary(item => item.Id, item => item.PositionName, StringComparer.OrdinalIgnoreCase);

        return employments
            .GroupBy(item => item.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<UserEmploymentResponse>)group.Select(item => new UserEmploymentResponse(
                    item.Id,
                    item.TenantId,
                    item.AppCode,
                    item.DeptId,
                    deptNames.TryGetValue(item.DeptId, out var deptName) ? deptName : null,
                    item.PositionId,
                    positionNames.TryGetValue(item.PositionId, out var positionName) ? positionName : null,
                    item.EmploymentName,
                    item.IsPrimary,
                    item.Status,
                    item.SortOrder)).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<SystemUserEmploymentEntity>> BuildEmploymentEntitiesAsync(
        string userId,
        UserUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var requested = (request.Employments ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.DeptId) || !string.IsNullOrWhiteSpace(item.PositionId))
            .ToList();
        if (requested.Count == 0 &&
            !string.IsNullOrWhiteSpace(request.DeptId) &&
            !string.IsNullOrWhiteSpace(request.PositionId))
        {
            requested.Add(new UserEmploymentRequest(null, null, null, request.DeptId!, request.PositionId!, null, true, request.Status, 1));
        }

        if (requested.Count == 0)
        {
            throw new ValidationException("用户至少需要配置一条有效任职");
        }

        var normalized = new List<UserEmploymentRequest>();
        foreach (var item in requested)
        {
            var deptId = NormalizeOptional(item.DeptId) ?? throw new ValidationException("任职部门不能为空");
            var positionId = NormalizeOptional(item.PositionId) ?? throw new ValidationException("任职岗位不能为空");
            normalized.Add(item with
            {
                TenantId = NormalizeOptional(item.TenantId) ?? "tenant-system",
                AppCode = (NormalizeOptional(item.AppCode) ?? "SYSTEM").ToUpperInvariant(),
                DeptId = deptId,
                PositionId = positionId,
                Status = NormalizeStatus(item.Status)
            });
        }

        var duplicate = normalized
            .GroupBy(item => $"{item.TenantId}|{item.AppCode}|{item.DeptId}|{item.PositionId}", StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ValidationException("同一用户不能重复配置相同部门岗位任职");
        }

        var activeEmployments = normalized
            .Where(item => string.Equals(item.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (activeEmployments.Count == 0)
        {
            throw new ValidationException("用户至少需要一条启用任职");
        }

        if (activeEmployments.Count(item => item.IsPrimary) == 0)
        {
            var first = activeEmployments[0];
            normalized[normalized.IndexOf(first)] = first with { IsPrimary = true };
        }

        if (activeEmployments.Count(item => item.IsPrimary) > 1)
        {
            throw new ValidationException("用户只能有一条主任职");
        }

        var deptIds = normalized.Select(item => item.DeptId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var positionIds = normalized.Select(item => item.PositionId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var departments = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => deptIds.Contains(item.Id) && !item.IsDeleted && item.Status == "Enabled")
            .ToListAsync(cancellationToken);
        var positions = await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
            .Where(item => positionIds.Contains(item.Id) && !item.IsDeleted && item.Status == "Enabled")
            .ToListAsync(cancellationToken);
        var departmentMap = departments.ToDictionary(item => item.Id, item => item, StringComparer.OrdinalIgnoreCase);
        var positionMap = positions.ToDictionary(item => item.Id, item => item, StringComparer.OrdinalIgnoreCase);

        var result = new List<SystemUserEmploymentEntity>();
        foreach (var item in normalized)
        {
            if (!departmentMap.TryGetValue(item.DeptId, out var department))
            {
                throw new ValidationException("任职部门不存在或已停用");
            }

            if (!positionMap.TryGetValue(item.PositionId, out var position))
            {
                throw new ValidationException("任职岗位不存在或已停用");
            }

            if (!string.Equals(position.DeptId, item.DeptId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("任职岗位不属于所选部门");
            }

            result.Add(new SystemUserEmploymentEntity
            {
                Id = NormalizeOptional(item.Id) ?? Guid.NewGuid().ToString("N"),
                UserId = userId,
                TenantId = item.TenantId!,
                AppCode = item.AppCode!,
                DeptId = item.DeptId,
                PositionId = item.PositionId,
                EmploymentName = NormalizeOptional(item.EmploymentName) ?? $"{department.DeptName}/{position.PositionName}",
                IsPrimary = item.IsPrimary && string.Equals(item.Status, "Enabled", StringComparison.OrdinalIgnoreCase),
                Status = item.Status,
                SortOrder = Math.Max(0, item.SortOrder)
            });
        }

        return result;
    }

    private async Task ReplaceEmploymentsAsync(
        string userId,
        IReadOnlyList<SystemUserEmploymentEntity> employments,
        CancellationToken cancellationToken)
    {
        var existing = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var updatedTime = DateTime.UtcNow;
        foreach (var item in existing)
        {
            item.IsDeleted = true;
            item.DeletedTime = updatedTime;
            item.UpdatedTime = updatedTime;
        }

        if (existing.Count > 0)
        {
            await databaseAccessor.GetCurrentDb().Updateable(existing)
                .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
        }

        foreach (var employment in employments)
        {
            employment.UserId = userId;
        }

        await InsertEmploymentsAsync(employments, cancellationToken);
    }

    private async Task InsertEmploymentsAsync(
        IReadOnlyList<SystemUserEmploymentEntity> employments,
        CancellationToken cancellationToken)
    {
        if (employments.Count == 0)
        {
            return;
        }

        await databaseAccessor.GetCurrentDb().Insertable(employments.ToArray()).ExecuteCommandAsync(cancellationToken);
    }

    private static UserEmploymentResponse? ResolvePrimaryEmployment(IReadOnlyList<UserEmploymentResponse>? employments) =>
        employments?.FirstOrDefault(item => item.IsPrimary && string.Equals(item.Status, "Enabled", StringComparison.OrdinalIgnoreCase)) ??
        employments?.FirstOrDefault(item => string.Equals(item.Status, "Enabled", StringComparison.OrdinalIgnoreCase)) ??
        employments?.FirstOrDefault();

    private static SystemUserEmploymentEntity? ResolvePrimaryEmployment(IReadOnlyList<SystemUserEmploymentEntity> employments) =>
        employments.FirstOrDefault(item => item.IsPrimary && string.Equals(item.Status, "Enabled", StringComparison.OrdinalIgnoreCase)) ??
        employments.FirstOrDefault(item => string.Equals(item.Status, "Enabled", StringComparison.OrdinalIgnoreCase)) ??
        employments.FirstOrDefault();

    private static string BuildEmploymentSummary(IReadOnlyList<UserEmploymentResponse> employments) =>
        employments.Count == 0
            ? string.Empty
            : string.Join("、", employments
                .Where(item => string.Equals(item.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.SortOrder)
                .Select(item => string.IsNullOrWhiteSpace(item.DeptName) && string.IsNullOrWhiteSpace(item.PositionName)
                    ? item.EmploymentName
                    : $"{item.DeptName ?? item.DeptId}/{item.PositionName ?? item.PositionId}"));

    private static string NormalizeStatus(string status)
    {
        return UserDomainPolicy.NormalizeStatus(status);
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

    private static string ResolveDataScope(SystemUserEntity user, IReadOnlyList<SystemRoleEntity> roles)
    {
        if (user.IsAdmin)
        {
            return "ALL";
        }

        if (roles.Count == 0)
        {
            return "SELF";
        }

        return UserDomainPolicy.ResolveDataScope(
            user.IsAdmin,
            roles.Select(role => role.DataScope).ToArray());
    }

    private static ISugarQueryable<SystemUserEntity> ApplyDefaultSort(ISugarQueryable<SystemUserEntity> query) =>
        query.OrderBy(item => item.CreatedTime, OrderByType.Desc);
}

