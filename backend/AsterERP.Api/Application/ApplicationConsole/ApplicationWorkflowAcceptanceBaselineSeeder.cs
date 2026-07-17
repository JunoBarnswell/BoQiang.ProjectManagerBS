using System.Text.Json;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationWorkflowAcceptanceBaselineSeeder(IPasswordHashService passwordHashService)
{
    private const string AcceptanceRuntimePageCode = "codex_microflow_order_demo";

    private static readonly string[] AcceptanceLeaderUserIds =
    [
        "wf_manager_approver",
        "wf_position_approver",
        "wf_dept_approver"
    ];

    private static readonly string[] AcceptanceRuntimePagePermissions =
    [
        PermissionCodes.BuildAppRuntimePagePermission(AcceptanceRuntimePageCode, "view"),
        PermissionCodes.BuildAppRuntimePagePermission(AcceptanceRuntimePageCode, "add"),
        PermissionCodes.BuildAppRuntimePagePermission(AcceptanceRuntimePageCode, "edit")
    ];

    private static readonly IReadOnlyDictionary<string, string> UserPasswords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["wf_starter"] = "starter123",
        ["wf_user_approver"] = "approve123",
        ["wf_role_approver"] = "roleapprove123",
        ["wf_dept_approver"] = "deptapprove123",
        ["wf_position_approver"] = "positionapprove123",
        ["wf_manager_approver"] = "managerapprove123",
        ["wf_delegate"] = "delegate123",
        ["wf_no_permission"] = "noperm123"
    };

    private static readonly string[] WorkflowUserPermissions =
    [
        PermissionCodes.WorkflowDraftQuery,
        PermissionCodes.WorkflowDraftEdit,
        PermissionCodes.WorkflowDraftDelete,
        PermissionCodes.WorkflowDraftSubmit,
        PermissionCodes.WorkflowFormQuery,
        PermissionCodes.WorkflowInstanceQuery,
        PermissionCodes.WorkflowInstanceStart,
        PermissionCodes.WorkflowTaskQuery,
        PermissionCodes.WorkflowTaskClaim,
        PermissionCodes.WorkflowTaskApprove,
        PermissionCodes.WorkflowTaskTransfer,
        PermissionCodes.WorkflowTaskDelegate,
        PermissionCodes.WorkflowTaskAttachment,
        PermissionCodes.WorkflowTaskComment,
        PermissionCodes.WorkflowHistoryQuery,
        PermissionCodes.WorkflowParticipantQuery,
        PermissionCodes.WorkflowDelegationQuery,
        PermissionCodes.WorkflowDelegationEdit,
        PermissionCodes.WorkflowDelegationDelete
    ];

    public async Task SeedAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(tenantId, "tenant-a", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(appCode, "MES", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        await UpsertDepartmentsAsync(appDb, currentUserId, cancellationToken);
        await UpsertPositionsAsync(appDb, currentUserId, cancellationToken);
        var roles = await UpsertRolesAsync(appDb, tenantId, normalizedAppCode, currentUserId, cancellationToken);
        await GrantWorkflowPermissionsAsync(appDb, roles, currentUserId, cancellationToken);
        await UpsertUsersAsync(appDb, currentUserId, cancellationToken);
        await UpsertUserRoleMappingsAsync(appDb, roles, tenantId, normalizedAppCode, currentUserId, cancellationToken);
    }

    private static async Task UpsertDepartmentsAsync(ISqlSugarClient appDb, string currentUserId, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new DepartmentDefinition("wf-root", "WFROOT", "审批验收组织", null, "Workflow Manager Approver", 900),
            new DepartmentDefinition("wf-finance", "WFFIN", "审批验收财务部", "wf-root", "Workflow Manager Approver", 910),
            new DepartmentDefinition("wf-sales", "WFSAL", "审批验收销售部", "wf-root", "Workflow Manager Approver", 920),
            new DepartmentDefinition("wf-audit", "WFAUD", "审批验收审计部", "wf-root", "Workflow Manager Approver", 930)
        };

        foreach (var definition in definitions)
        {
            var entity = await appDb.Queryable<SystemDepartmentEntity>()
                .FirstAsync(item => item.Id == definition.Id, cancellationToken);
            var now = DateTime.UtcNow;
            if (entity is null)
            {
                await appDb.Insertable(new SystemDepartmentEntity
                {
                    Id = definition.Id,
                    DeptCode = definition.Code,
                    DeptName = definition.Name,
                    ParentId = definition.ParentId,
                    ManagerName = definition.ManagerName,
                    LeaderUserIdsJson = definition.LeaderUserIds.Count == 0 ? null : JsonSerializer.Serialize(definition.LeaderUserIds),
                    SortOrder = definition.SortOrder,
                    Status = "Enabled",
                    CreatedBy = currentUserId,
                    CreatedTime = now,
                    IsDeleted = false
                }).ExecuteCommandAsync(cancellationToken);
                continue;
            }

            entity.DeptCode = definition.Code;
            entity.DeptName = definition.Name;
            entity.ParentId = definition.ParentId;
            entity.ManagerName = definition.ManagerName;
            entity.LeaderUserIdsJson = definition.LeaderUserIds.Count == 0 ? null : JsonSerializer.Serialize(definition.LeaderUserIds);
            entity.SortOrder = definition.SortOrder;
            entity.Status = "Enabled";
            entity.IsDeleted = false;
            entity.DeletedBy = null;
            entity.DeletedTime = null;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
            await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task UpsertPositionsAsync(ISqlSugarClient appDb, string currentUserId, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new PositionDefinition("wf-position-starter", "WFSTART", "审批发起岗", "wf-sales", "M2", 900),
            new PositionDefinition("wf-position-approver", "WFAPP", "审批专员岗", "wf-finance", "M3", 910),
            new PositionDefinition("wf-position-manager", "WFMGR", "审批负责岗", "wf-finance", "M4", 920)
        };

        foreach (var definition in definitions)
        {
            var entity = await appDb.Queryable<SystemPositionEntity>()
                .FirstAsync(item => item.Id == definition.Id, cancellationToken);
            var now = DateTime.UtcNow;
            if (entity is null)
            {
                await appDb.Insertable(new SystemPositionEntity
                {
                    Id = definition.Id,
                    PositionCode = definition.Code,
                    PositionName = definition.Name,
                    DeptId = definition.DeptId,
                    PositionLevel = definition.Level,
                    SortOrder = definition.SortOrder,
                    Status = "Enabled",
                    CreatedBy = currentUserId,
                    CreatedTime = now,
                    IsDeleted = false
                }).ExecuteCommandAsync(cancellationToken);
                continue;
            }

            entity.PositionCode = definition.Code;
            entity.PositionName = definition.Name;
            entity.DeptId = definition.DeptId;
            entity.PositionLevel = definition.Level;
            entity.SortOrder = definition.SortOrder;
            entity.Status = "Enabled";
            entity.IsDeleted = false;
            entity.DeletedBy = null;
            entity.DeletedTime = null;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
            await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyDictionary<string, SystemRoleEntity>> UpsertRolesAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new RoleDefinition("wf_starter", "Workflow Starter", "SELF"),
            new RoleDefinition("wf_user_approver", "Workflow User Approver", "SELF"),
            new RoleDefinition("wf_role_approver", "Workflow Role Approver", "SELF"),
            new RoleDefinition("wf_dept_approver", "Workflow Department Approver", "DEPT"),
            new RoleDefinition("wf_position_approver", "Workflow Position Approver", "SELF"),
            new RoleDefinition("wf_manager_approver", "Workflow Manager Approver", "DEPT"),
            new RoleDefinition("wf_delegate", "Workflow Delegate", "SELF"),
            new RoleDefinition("wf_no_permission", "Workflow No Permission", "SELF")
        };
        var roles = new Dictionary<string, SystemRoleEntity>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            var entity = await appDb.Queryable<SystemRoleEntity>()
                .FirstAsync(item =>
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.RoleCode == definition.Code,
                    cancellationToken);
            var now = DateTime.UtcNow;
            if (entity is null)
            {
                entity = new SystemRoleEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    TenantId = tenantId,
                    AppCode = appCode,
                    RoleCode = definition.Code,
                    RoleName = definition.Name,
                    DataScope = definition.DataScope,
                    IsEnabled = true,
                    CreatedBy = currentUserId,
                    CreatedTime = now,
                    IsDeleted = false
                };
                await appDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            }
            else
            {
                entity.RoleName = definition.Name;
                entity.DataScope = definition.DataScope;
                entity.IsEnabled = true;
                entity.IsDeleted = false;
                entity.DeletedBy = null;
                entity.DeletedTime = null;
                entity.UpdatedBy = currentUserId;
                entity.UpdatedTime = now;
                await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            }

            roles[definition.Code] = entity;
        }

        return roles;
    }

    private static async Task GrantWorkflowPermissionsAsync(
        ISqlSugarClient appDb,
        IReadOnlyDictionary<string, SystemRoleEntity> roles,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var workflowPermissions = WorkflowUserPermissions
            .Concat(AcceptanceRuntimePagePermissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var permissions = await appDb.Queryable<SystemPermissionCodeEntity>()
            .Where(item => workflowPermissions.Contains(item.PermissionCode) && !item.IsDeleted && item.IsEnabled)
            .ToListAsync(cancellationToken);
        var permissionIds = permissions.Select(item => item.Id).ToArray();
        foreach (var role in roles.Values.Where(item => !string.Equals(item.RoleCode, "wf_no_permission", StringComparison.OrdinalIgnoreCase)))
        {
            await GrantRolePermissionsAsync(appDb, role.Id, permissionIds, currentUserId, cancellationToken);
        }
    }

    private static async Task GrantRolePermissionsAsync(
        ISqlSugarClient appDb,
        string roleId,
        IReadOnlyCollection<string> permissionIds,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var existing = await appDb.Queryable<SystemRolePermissionEntity>()
            .Where(item => item.RoleId == roleId && permissionIds.Contains(item.PermissionCodeId))
            .ToListAsync(cancellationToken);
        var existingIds = existing.Select(item => item.PermissionCodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var inserts = permissionIds
            .Where(item => !existingIds.Contains(item))
            .Select(permissionId => new SystemRolePermissionEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                RoleId = roleId,
                PermissionCodeId = permissionId,
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

        if (inserts.Count > 0)
        {
            await appDb.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }

        var updates = existing.Where(item => !item.IsDeleted && item.UpdatedTime == now).ToList();
        if (updates.Count > 0)
        {
            await appDb.Updateable(updates).ExecuteCommandAsync(cancellationToken);
        }
    }

    private async Task UpsertUsersAsync(ISqlSugarClient appDb, string currentUserId, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new UserDefinition("wf_starter", "Workflow Starter", "wf-sales", "wf-position-starter", "wf_starter@example.local"),
            new UserDefinition("wf_user_approver", "Workflow User Approver", "wf-root", "wf-position-approver", "wf_user_approver@example.local"),
            new UserDefinition("wf_role_approver", "Workflow Role Approver", "wf-root", "wf-position-approver", "wf_role_approver@example.local"),
            new UserDefinition("wf_dept_approver", "Workflow Department Approver", "wf-finance", "wf-position-approver", "wf_dept_approver@example.local"),
            new UserDefinition("wf_position_approver", "Workflow Position Approver", "wf-finance", "wf-position-manager", "wf_position_approver@example.local"),
            new UserDefinition("wf_manager_approver", "Workflow Manager Approver", "wf-finance", "wf-position-manager", "wf_manager_approver@example.local"),
            new UserDefinition("wf_delegate", "Workflow Delegate", "wf-audit", "wf-position-approver", "wf_delegate@example.local"),
            new UserDefinition("wf_no_permission", "Workflow No Permission", "wf-root", "wf-position-approver", "wf_no_permission@example.local")
        };

        foreach (var definition in definitions)
        {
            var entity = await appDb.Queryable<SystemUserEntity>()
                .FirstAsync(item => item.UserName == definition.UserName, cancellationToken);
            var now = DateTime.UtcNow;
            if (entity is null)
            {
                await appDb.Insertable(new SystemUserEntity
                {
                    Id = definition.UserName,
                    UserName = definition.UserName,
                    DisplayName = definition.DisplayName,
                    PasswordHash = passwordHashService.HashPassword(UserPasswords[definition.UserName]),
                    DeptId = definition.DeptId,
                    PositionId = definition.PositionId,
                    Email = definition.Email,
                    IsAdmin = false,
                    Status = "Enabled",
                    CreatedBy = currentUserId,
                    CreatedTime = now,
                    IsDeleted = false
                }).ExecuteCommandAsync(cancellationToken);
                continue;
            }

            entity.DisplayName = definition.DisplayName;
            entity.PasswordHash = passwordHashService.HashPassword(UserPasswords[definition.UserName]);
            entity.DeptId = definition.DeptId;
            entity.PositionId = definition.PositionId;
            entity.Email = definition.Email;
            entity.IsAdmin = false;
            entity.Status = "Enabled";
            entity.IsDeleted = false;
            entity.DeletedBy = null;
            entity.DeletedTime = null;
            entity.UpdatedBy = currentUserId;
            entity.UpdatedTime = now;
            await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task UpsertUserRoleMappingsAsync(
        ISqlSugarClient appDb,
        IReadOnlyDictionary<string, SystemRoleEntity> roles,
        string tenantId,
        string appCode,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        foreach (var userName in UserPasswords.Keys)
        {
            if (!roles.TryGetValue(userName, out var role))
            {
                continue;
            }

            await UpsertUserRoleAsync(appDb, userName, role.Id, currentUserId, cancellationToken);
            await UpsertUserAppRoleAsync(appDb, userName, role.Id, tenantId, appCode, currentUserId, cancellationToken);
        }
    }

    private static async Task UpsertUserRoleAsync(
        ISqlSugarClient appDb,
        string userId,
        string roleId,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var entity = await appDb.Queryable<SystemUserRoleEntity>()
            .FirstAsync(item => item.UserId == userId && item.RoleId == roleId, cancellationToken);
        var now = DateTime.UtcNow;
        if (entity is null)
        {
            await appDb.Insertable(new SystemUserRoleEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                RoleId = roleId,
                CreatedBy = currentUserId,
                CreatedTime = now,
                IsDeleted = false
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        entity.IsDeleted = false;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
        entity.UpdatedBy = currentUserId;
        entity.UpdatedTime = now;
        await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task UpsertUserAppRoleAsync(
        ISqlSugarClient appDb,
        string userId,
        string roleId,
        string tenantId,
        string appCode,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var entity = await appDb.Queryable<SystemUserAppRoleEntity>()
            .FirstAsync(item => item.UserId == userId && item.TenantId == tenantId && item.AppCode == appCode && item.RoleId == roleId, cancellationToken);
        var now = DateTime.UtcNow;
        if (entity is null)
        {
            await appDb.Insertable(new SystemUserAppRoleEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                TenantId = tenantId,
                AppCode = appCode,
                RoleId = roleId,
                IsDefault = true,
                CreatedBy = currentUserId,
                CreatedTime = now,
                IsDeleted = false
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        entity.IsDefault = true;
        entity.IsDeleted = false;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
        entity.UpdatedBy = currentUserId;
        entity.UpdatedTime = now;
        await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private sealed record DepartmentDefinition(string Id, string Code, string Name, string? ParentId, string ManagerName, int SortOrder)
    {
        public IReadOnlyList<string> LeaderUserIds { get; init; } = AcceptanceLeaderUserIds;
    }

    private sealed record PositionDefinition(string Id, string Code, string Name, string DeptId, string Level, int SortOrder);

    private sealed record RoleDefinition(string Code, string Name, string DataScope);

    private sealed record UserDefinition(string UserName, string DisplayName, string DeptId, string PositionId, string Email);
}
