using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationRbacBaselineSeeder
{
    private const string AppAdminRoleCode = "app_admin";
    private const string AppAdminRoleName = "应用管理员";
    private const string DefaultDepartmentId = "app-default-department";
    private const string DefaultDepartmentCode = "APP_DEFAULT";
    private const string DefaultDepartmentName = "默认部门";
    private const string DefaultPositionId = "app-default-position";
    private const string DefaultPositionCode = "APP_ADMIN";
    private const string DefaultPositionName = "应用管理员";
    private const string RetiredAdminCenterPermissionCode = "app:admin-center:view";

    public async Task SeedAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        SystemUserEntity currentUser,
        CancellationToken cancellationToken)
    {
        var permissionByCode = await UpsertPermissionCodesAsync(appDb, currentUser.Id, cancellationToken);
        await SoftDeleteRetiredPermissionsAsync(appDb, currentUser.Id, cancellationToken);
        var role = await UpsertAppAdminRoleAsync(appDb, tenantId, appCode, currentUser.Id, cancellationToken);
        await GrantAllPermissionsAsync(appDb, role.Id, permissionByCode.Values.Select(item => item.Id).ToArray(), currentUser.Id, cancellationToken);
        var department = await UpsertDefaultDepartmentAsync(appDb, currentUser.Id, cancellationToken);
        var position = await UpsertDefaultPositionAsync(appDb, department.Id, currentUser.Id, cancellationToken);
        await UpsertCurrentUserAsync(appDb, currentUser, department.Id, position.Id, cancellationToken);
        await UpsertUserRoleAsync(appDb, currentUser.Id, role.Id, tenantId, appCode, cancellationToken);
        await UpsertUserEmploymentAsync(appDb, currentUser.Id, tenantId, appCode, department, position, cancellationToken);
    }

    private static async Task<Dictionary<string, SystemPermissionCodeEntity>> UpsertPermissionCodesAsync(
        ISqlSugarClient appDb,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var definitions = ApplicationShellPermissionCatalog.Definitions
            .GroupBy(item => item.PermissionCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var codes = definitions.Select(item => item.PermissionCode).ToArray();
        var existing = await appDb.Queryable<SystemPermissionCodeEntity>()
            .Where(item => codes.Contains(item.PermissionCode))
            .ToListAsync(cancellationToken);
        var byCode = new Dictionary<string, SystemPermissionCodeEntity>(StringComparer.OrdinalIgnoreCase);
        var duplicatePermissions = new List<SystemPermissionCodeEntity>();
        foreach (var group in existing.GroupBy(item => item.PermissionCode, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group
                .OrderBy(item => item.IsDeleted)
                .ThenBy(item => item.CreatedTime)
                .ToArray();
            byCode[group.Key] = ordered[0];
            duplicatePermissions.AddRange(ordered.Skip(1));
        }

        var now = DateTime.UtcNow;
        var inserts = new List<SystemPermissionCodeEntity>();
        var updates = new List<SystemPermissionCodeEntity>();

        foreach (var definition in definitions)
        {
            if (!byCode.TryGetValue(definition.PermissionCode, out var entity))
            {
                entity = new SystemPermissionCodeEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ModuleName = definition.ModuleName,
                    PermissionCode = definition.PermissionCode,
                    PermissionName = definition.PermissionName,
                    IsEnabled = true,
                    CreatedBy = currentUserId,
                    CreatedTime = now,
                    IsDeleted = false
                };
                inserts.Add(entity);
                byCode[definition.PermissionCode] = entity;
                continue;
            }

            entity.ModuleName = definition.ModuleName;
            entity.PermissionName = definition.PermissionName;
            entity.IsEnabled = true;
            entity.IsDeleted = false;
            entity.DeletedBy = null;
            entity.DeletedTime = null;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
            updates.Add(entity);
        }

        if (inserts.Count > 0)
        {
            await appDb.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }

        if (updates.Count > 0)
        {
            await appDb.Updateable(updates).ExecuteCommandAsync(cancellationToken);
        }

        await SoftDeleteDuplicatePermissionCodesAsync(appDb, duplicatePermissions, currentUserId, cancellationToken);
        return byCode;
    }

    private static async Task SoftDeleteDuplicatePermissionCodesAsync(
        ISqlSugarClient appDb,
        IReadOnlyCollection<SystemPermissionCodeEntity> duplicatePermissions,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        if (duplicatePermissions.Count == 0)
        {
            return;
        }

        var duplicateIds = duplicatePermissions.Select(item => item.Id).ToArray();
        var rolePermissions = await appDb.Queryable<SystemRolePermissionEntity>()
            .Where(item => duplicateIds.Contains(item.PermissionCodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var entity in duplicatePermissions)
        {
            entity.IsEnabled = false;
            entity.IsDeleted = true;
            entity.DeletedBy = currentUserId;
            entity.DeletedTime = now;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
        }

        foreach (var entity in rolePermissions)
        {
            entity.IsDeleted = true;
            entity.DeletedBy = currentUserId;
            entity.DeletedTime = now;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
        }

        await appDb.Updateable(duplicatePermissions.ToList()).ExecuteCommandAsync(cancellationToken);
        if (rolePermissions.Count > 0)
        {
            await appDb.Updateable(rolePermissions).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task SoftDeleteRetiredPermissionsAsync(
        ISqlSugarClient appDb,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var retiredPermissions = await appDb.Queryable<SystemPermissionCodeEntity>()
            .Where(item => item.PermissionCode == RetiredAdminCenterPermissionCode && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (retiredPermissions.Count == 0)
        {
            return;
        }

        var retiredPermissionIds = retiredPermissions.Select(item => item.Id).ToArray();
        var rolePermissions = await appDb.Queryable<SystemRolePermissionEntity>()
            .Where(item => retiredPermissionIds.Contains(item.PermissionCodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var entity in retiredPermissions)
        {
            entity.IsEnabled = false;
            entity.IsDeleted = true;
            entity.DeletedBy = currentUserId;
            entity.DeletedTime = now;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
        }

        foreach (var entity in rolePermissions)
        {
            entity.IsDeleted = true;
            entity.DeletedBy = currentUserId;
            entity.DeletedTime = now;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
        }

        await appDb.Updateable(retiredPermissions).ExecuteCommandAsync(cancellationToken);
        if (rolePermissions.Count > 0)
        {
            await appDb.Updateable(rolePermissions).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task<SystemRoleEntity> UpsertAppAdminRoleAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var role = (await appDb.Queryable<SystemRoleEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.RoleCode == AppAdminRoleCode)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (role is null)
        {
            role = new SystemRoleEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                AppCode = appCode,
                RoleName = AppAdminRoleName,
                RoleCode = AppAdminRoleCode,
                DataScope = "ALL",
                IsEnabled = true,
                CreatedBy = currentUserId,
                CreatedTime = now,
                IsDeleted = false
            };
            await appDb.Insertable(role).ExecuteCommandAsync(cancellationToken);
            return role;
        }

        role.RoleName = AppAdminRoleName;
        role.DataScope = "ALL";
        role.IsEnabled = true;
        role.IsDeleted = false;
        role.DeletedBy = null;
        role.DeletedTime = null;
        role.UpdatedBy = currentUserId;
        role.UpdatedTime = now;
        await appDb.Updateable(role).ExecuteCommandAsync(cancellationToken);
        return role;
    }

    private static async Task GrantAllPermissionsAsync(
        ISqlSugarClient appDb,
        string roleId,
        IReadOnlyCollection<string> permissionCodeIds,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        if (permissionCodeIds.Count == 0)
        {
            return;
        }

        var existing = await appDb.Queryable<SystemRolePermissionEntity>()
            .Where(item => item.RoleId == roleId && permissionCodeIds.Contains(item.PermissionCodeId))
            .ToListAsync(cancellationToken);
        var existingIds = existing.Select(item => item.PermissionCodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var inserts = permissionCodeIds
            .Where(permissionCodeId => !existingIds.Contains(permissionCodeId))
            .Select(permissionCodeId => new SystemRolePermissionEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                RoleId = roleId,
                PermissionCodeId = permissionCodeId,
                CreatedBy = currentUserId,
                CreatedTime = now,
                IsDeleted = false
            })
            .ToList();

        foreach (var entity in existing.Where(item => item.IsDeleted))
        {
            entity.IsDeleted = false;
            entity.DeletedBy = null;
            entity.DeletedTime = null;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
        }

        var updates = existing.Where(item => item.IsDeleted == false && item.UpdatedBy == currentUserId && item.UpdatedTime == now).ToList();
        if (inserts.Count > 0)
        {
            await appDb.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }

        if (updates.Count > 0)
        {
            await appDb.Updateable(updates).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task<SystemDepartmentEntity> UpsertDefaultDepartmentAsync(
        ISqlSugarClient appDb,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var department = (await appDb.Queryable<SystemDepartmentEntity>()
            .Where(item => item.Id == DefaultDepartmentId || item.DeptCode == DefaultDepartmentCode)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (department is null)
        {
            department = new SystemDepartmentEntity
            {
                Id = DefaultDepartmentId,
                DeptCode = DefaultDepartmentCode,
                DeptName = DefaultDepartmentName,
                ParentId = null,
                SortOrder = 1,
                Status = "Enabled",
                CreatedBy = currentUserId,
                CreatedTime = now,
                IsDeleted = false
            };
            await appDb.Insertable(department).ExecuteCommandAsync(cancellationToken);
            return department;
        }

        if (department.IsDeleted || !string.Equals(department.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            department.DeptCode = string.IsNullOrWhiteSpace(department.DeptCode) ? DefaultDepartmentCode : department.DeptCode;
            department.DeptName = string.IsNullOrWhiteSpace(department.DeptName) ? DefaultDepartmentName : department.DeptName;
            department.Status = "Enabled";
            department.IsDeleted = false;
            department.DeletedBy = null;
            department.DeletedTime = null;
            department.UpdatedBy = currentUserId;
            department.UpdatedTime = now;
            await appDb.Updateable(department).ExecuteCommandAsync(cancellationToken);
        }

        return department;
    }

    private static async Task<SystemPositionEntity> UpsertDefaultPositionAsync(
        ISqlSugarClient appDb,
        string departmentId,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var position = (await appDb.Queryable<SystemPositionEntity>()
            .Where(item => item.Id == DefaultPositionId || item.PositionCode == DefaultPositionCode)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (position is null)
        {
            position = new SystemPositionEntity
            {
                Id = DefaultPositionId,
                PositionCode = DefaultPositionCode,
                PositionName = DefaultPositionName,
                DeptId = departmentId,
                PositionLevel = "L1",
                SortOrder = 1,
                Status = "Enabled",
                CreatedBy = currentUserId,
                CreatedTime = now,
                IsDeleted = false
            };
            await appDb.Insertable(position).ExecuteCommandAsync(cancellationToken);
            return position;
        }

        if (position.IsDeleted || !string.Equals(position.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            position.PositionCode = string.IsNullOrWhiteSpace(position.PositionCode) ? DefaultPositionCode : position.PositionCode;
            position.PositionName = string.IsNullOrWhiteSpace(position.PositionName) ? DefaultPositionName : position.PositionName;
            position.DeptId = string.IsNullOrWhiteSpace(position.DeptId) ? departmentId : position.DeptId;
            position.Status = "Enabled";
            position.IsDeleted = false;
            position.DeletedBy = null;
            position.DeletedTime = null;
            position.UpdatedBy = currentUserId;
            position.UpdatedTime = now;
            await appDb.Updateable(position).ExecuteCommandAsync(cancellationToken);
        }

        return position;
    }

    private static async Task UpsertCurrentUserAsync(
        ISqlSugarClient appDb,
        SystemUserEntity currentUser,
        string departmentId,
        string positionId,
        CancellationToken cancellationToken)
    {
        var existing = (await appDb.Queryable<SystemUserEntity>()
            .Where(item => item.Id == currentUser.Id)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (existing is null)
        {
            var clone = new SystemUserEntity
            {
                Id = currentUser.Id,
                UserName = currentUser.UserName,
                DisplayName = currentUser.DisplayName,
                PasswordHash = currentUser.PasswordHash,
                PhoneNumber = currentUser.PhoneNumber,
                Email = currentUser.Email,
                DeptId = departmentId,
                PositionId = positionId,
                IsAdmin = currentUser.IsAdmin,
                Status = currentUser.Status,
                CreatedBy = currentUser.CreatedBy,
                CreatedTime = currentUser.CreatedTime == default ? now : currentUser.CreatedTime,
                IsDeleted = false
            };
            await appDb.Insertable(clone).ExecuteCommandAsync(cancellationToken);
            return;
        }

        existing.UserName = currentUser.UserName;
        existing.DisplayName = currentUser.DisplayName;
        existing.PasswordHash = currentUser.PasswordHash;
        existing.PhoneNumber = currentUser.PhoneNumber;
        existing.Email = currentUser.Email;
        if (string.IsNullOrWhiteSpace(existing.DeptId))
        {
            existing.DeptId = departmentId;
        }

        if (string.IsNullOrWhiteSpace(existing.PositionId))
        {
            existing.PositionId = positionId;
        }

        existing.IsAdmin = currentUser.IsAdmin;
        existing.Status = currentUser.Status;
        existing.IsDeleted = false;
        existing.DeletedBy = null;
        existing.DeletedTime = null;
        existing.UpdatedBy = currentUser.Id;
        existing.UpdatedTime = now;
        await appDb.Updateable(existing).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task UpsertUserRoleAsync(
        ISqlSugarClient appDb,
        string userId,
        string roleId,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userRole = (await appDb.Queryable<SystemUserRoleEntity>()
            .Where(item => item.UserId == userId && item.RoleId == roleId)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (userRole is null)
        {
            await appDb.Insertable(new SystemUserRoleEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                RoleId = roleId,
                CreatedBy = userId,
                CreatedTime = now,
                IsDeleted = false
            }).ExecuteCommandAsync(cancellationToken);
        }
        else if (userRole.IsDeleted)
        {
            userRole.IsDeleted = false;
            userRole.DeletedBy = null;
            userRole.DeletedTime = null;
            userRole.UpdatedBy = userId;
            userRole.UpdatedTime = now;
            await appDb.Updateable(userRole).ExecuteCommandAsync(cancellationToken);
        }

        var userAppRole = (await appDb.Queryable<SystemUserAppRoleEntity>()
            .Where(item => item.UserId == userId && item.TenantId == tenantId && item.AppCode == appCode && item.RoleId == roleId)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (userAppRole is null)
        {
            await appDb.Insertable(new SystemUserAppRoleEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                TenantId = tenantId,
                AppCode = appCode,
                RoleId = roleId,
                IsDefault = true,
                CreatedBy = userId,
                CreatedTime = now,
                IsDeleted = false
            }).ExecuteCommandAsync(cancellationToken);
        }
        else if (userAppRole.IsDeleted)
        {
            userAppRole.IsDeleted = false;
            userAppRole.DeletedBy = null;
            userAppRole.DeletedTime = null;
            userAppRole.UpdatedBy = userId;
            userAppRole.UpdatedTime = now;
            await appDb.Updateable(userAppRole).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task UpsertUserEmploymentAsync(
        ISqlSugarClient appDb,
        string userId,
        string tenantId,
        string appCode,
        SystemDepartmentEntity department,
        SystemPositionEntity position,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var employment = (await appDb.Queryable<SystemUserEmploymentEntity>()
            .Where(item =>
                item.UserId == userId &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.DeptId == department.Id &&
                item.PositionId == position.Id)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (employment is null)
        {
            await appDb.Insertable(new SystemUserEmploymentEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                TenantId = tenantId,
                AppCode = appCode,
                DeptId = department.Id,
                PositionId = position.Id,
                EmploymentName = $"{department.DeptName}/{position.PositionName}",
                IsPrimary = true,
                Status = "Enabled",
                SortOrder = 1,
                CreatedBy = userId,
                CreatedTime = now,
                IsDeleted = false
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        employment.EmploymentName = string.IsNullOrWhiteSpace(employment.EmploymentName)
            ? $"{department.DeptName}/{position.PositionName}"
            : employment.EmploymentName;
        employment.IsPrimary = true;
        employment.Status = "Enabled";
        employment.IsDeleted = false;
        employment.DeletedBy = null;
        employment.DeletedTime = null;
        employment.UpdatedBy = userId;
        employment.UpdatedTime = now;
        await appDb.Updateable(employment).ExecuteCommandAsync(cancellationToken);
    }
}
