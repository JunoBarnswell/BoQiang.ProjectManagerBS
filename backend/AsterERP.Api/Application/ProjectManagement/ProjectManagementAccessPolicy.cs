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
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        if (currentUser.IsAsterErpPlatformAdmin() || currentUser.HasAsterErpPermission("*")) return;
        var userId = RequireUserId();
        var db = databaseAccessor.GetProjectManagementDb();
        var visible = await db.Queryable<ProjectManagementProjectEntity>()
            .Where(project => project.Id == projectId && !project.IsDeleted &&
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
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        if (currentUser.IsAsterErpPlatformAdmin() || currentUser.HasAsterErpPermission("*")) return;
        var userId = RequireUserId();
        var db = databaseAccessor.GetProjectManagementDb();
        var project = await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        var owner = project.FirstOrDefault()?.OwnerUserId;
        if (string.Equals(owner, userId, StringComparison.OrdinalIgnoreCase)) return;
        var member = await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => item.ProjectId == projectId && item.UserId == userId && item.IsActive && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        var role = member.FirstOrDefault()?.RoleCode;
        if (role is "Owner" or "Manager" or "Lead") return;
        if (role == "Member" && string.Equals(assigneeUserId, userId, StringComparison.OrdinalIgnoreCase)) return;
        throw new ValidationException("当前项目角色不能修改该任务", ErrorCodes.PermissionDenied);
    }

    public async Task EnsureCanManageDependenciesAsync(string projectId, CancellationToken cancellationToken = default)
        => await EnsureRoleAsync(projectId, ["Owner", "Manager", "Lead"], "当前项目角色不能管理任务依赖", cancellationToken);

    /// <summary>
    /// 统一父任务完成门禁。<paramref name="completingTaskIds"/> 可包含同一批次内一并完成的子任务，
    /// 其余未完成直接子任务仍要求显式强制完成权限和原因。
    /// </summary>
    public async Task EnsureCanCompleteTasksAsync(
        string projectId,
        IReadOnlyCollection<string> parentTaskIds,
        IReadOnlySet<string> completingTaskIds,
        bool forceComplete,
        string? forceCompleteReason,
        CancellationToken cancellationToken = default)
    {
        if (parentTaskIds.Count == 0) return;
        var parentIds = parentTaskIds.Distinct(StringComparer.Ordinal).ToList();
        var incompleteChildren = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && parentIds.Contains(item.ParentTaskId!) && !item.IsDeleted && item.Status != ProjectManagementDomainRules.TaskDone)
            .ToListAsync(cancellationToken);
        if (!incompleteChildren.Any(item => !completingTaskIds.Contains(item.Id))) return;
        if (!forceComplete) throw new ValidationException("存在未完成子任务，不能完成父任务");
        if (string.IsNullOrWhiteSpace(forceCompleteReason)) throw new ValidationException("强制完成父任务必须填写原因");
        if (!currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementTaskOverrideWip))
            throw new ValidationException("没有强制完成父任务权限", ErrorCodes.PermissionDenied);
        await EnsureCanManageProjectAsync(projectId, cancellationToken);
    }

    private async Task EnsureRoleAsync(string projectId, string[] roles, string message, CancellationToken cancellationToken, bool includeDeleted = false)
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        if (currentUser.IsAsterErpPlatformAdmin() || currentUser.HasAsterErpPermission("*")) return;
        var userId = RequireUserId();
        var db = databaseAccessor.GetProjectManagementDb();
        var project = await db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && (includeDeleted || !item.IsDeleted))
            .Take(1)
            .ToListAsync(cancellationToken);
        if (string.Equals(project.FirstOrDefault()?.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase)) return;
        var allowed = (await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => item.ProjectId == projectId && item.UserId == userId && item.IsActive && !item.IsDeleted).ToListAsync(cancellationToken)).Any(item => roles.Contains(item.RoleCode, StringComparer.OrdinalIgnoreCase));
        if (!allowed) throw new ValidationException(message, ErrorCodes.PermissionDenied);
    }

    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
}
