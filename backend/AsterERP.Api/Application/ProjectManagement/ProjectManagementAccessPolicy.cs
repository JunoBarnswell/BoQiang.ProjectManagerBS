using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 项目域的第二层授权边界：Controller 权限码负责功能入口，本策略负责项目角色和对象级写权限。
/// </summary>
public sealed class ProjectManagementAccessPolicy(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser)
{
    public async Task EnsureCanViewProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        if (currentUser.IsAsterErpPlatformAdmin() || currentUser.HasAsterErpPermission("*")) return;
        var userId = RequireUserId();
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var db = databaseAccessor.GetProjectManagementDb();
        var visible = await db.Queryable<ProjectManagementProjectEntity>()
            .Where(project => project.Id == projectId && project.TenantId == tenantId && project.AppCode == appCode && !project.IsDeleted &&
                (project.OwnerUserId == userId || SqlSugar.SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                    .Where(member => member.ProjectId == project.Id && member.UserId == userId && member.IsActive && !member.IsDeleted).Any()))
            .AnyAsync(cancellationToken);
        if (!visible) throw new ValidationException("当前用户无权查看该项目", ErrorCodes.PermissionDenied);
    }

    public async Task EnsureCanManageProjectAsync(string projectId, CancellationToken cancellationToken = default)
        => await EnsureRoleAsync(projectId, ["Owner", "Manager"], "当前项目角色不能修改项目", cancellationToken);

    /// <summary>
    /// 回收站操作仍沿用项目 Owner/Manager 的对象级授权，但被删除项目不再被默认的活动项目判断排除。
    /// </summary>
    public async Task EnsureCanManageDeletedProjectAsync(string projectId, CancellationToken cancellationToken = default)
        => await EnsureRoleAsync(projectId, ["Owner", "Manager"], "当前项目角色不能执行回收站操作", cancellationToken, includeDeleted: true);

    public async Task EnsureCanManageMembersAsync(string projectId, CancellationToken cancellationToken = default)
        => await EnsureRoleAsync(projectId, ["Owner", "Manager"], "当前项目角色不能管理成员", cancellationToken);

    public async Task EnsureCanManageTaskAsync(string projectId, string? assigneeUserId = null, CancellationToken cancellationToken = default)
    {
        if (currentUser.IsAsterErpPlatformAdmin() || currentUser.HasAsterErpPermission("*")) return;
        var userId = RequireUserId();
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var db = databaseAccessor.GetProjectManagementDb();
        var project = await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        var owner = project.FirstOrDefault()?.OwnerUserId;
        if (string.Equals(owner, userId, StringComparison.OrdinalIgnoreCase)) return;
        var member = await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && item.UserId == userId && item.IsActive && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        var role = member.FirstOrDefault()?.RoleCode;
        if (role is "Owner" or "Manager" or "Lead") return;
        if (role == "Member" && string.Equals(assigneeUserId, userId, StringComparison.OrdinalIgnoreCase)) return;
        throw new ValidationException("当前项目角色不能修改该任务", ErrorCodes.PermissionDenied);
    }

    public async Task EnsureCanManageDependenciesAsync(string projectId, CancellationToken cancellationToken = default)
        => await EnsureRoleAsync(projectId, ["Owner", "Manager", "Lead"], "当前项目角色不能管理任务依赖", cancellationToken);

    private async Task EnsureRoleAsync(string projectId, string[] roles, string message, CancellationToken cancellationToken, bool includeDeleted = false)
    {
        if (currentUser.IsAsterErpPlatformAdmin() || currentUser.HasAsterErpPermission("*")) return;
        var userId = RequireUserId();
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var db = databaseAccessor.GetProjectManagementDb();
        var project = await db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && item.AppCode == appCode && (includeDeleted || !item.IsDeleted))
            .Take(1)
            .ToListAsync(cancellationToken);
        if (string.Equals(project.FirstOrDefault()?.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase)) return;
        var allowed = (await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && item.UserId == userId && item.IsActive && !item.IsDeleted).ToListAsync(cancellationToken)).Any(item => roles.Contains(item.RoleCode, StringComparer.OrdinalIgnoreCase));
        if (!allowed) throw new ValidationException(message, ErrorCodes.PermissionDenied);
    }

    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private static string RequireAppCode() => ProjectManagementPlatformScope.AppCode;
}
