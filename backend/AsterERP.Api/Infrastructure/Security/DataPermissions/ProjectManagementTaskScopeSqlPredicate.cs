using System.Linq.Expressions;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

/// <summary>
/// 项目任务范围的唯一 ORM 谓词。Owner/Manager 和未绑定范围的 Lead 保持项目全范围；
/// 绑定 ScopeRootTaskId 的 Lead 仅能匹配该根任务及最大五层任务树中的后代。
/// </summary>
internal static class ProjectManagementTaskScopeSqlPredicate
{
    public static Expression<Func<ProjectManagementTaskEntity, bool>> Create(
        string tenantId,
        string appCode,
        string? userId,
        bool restrictToMembership) => task =>
        task.TenantId == tenantId && task.AppCode == appCode &&
        (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
            .Where(project => project.Id == task.ProjectId && project.TenantId == tenantId && project.AppCode == appCode &&
                (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                    .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == appCode &&
                        member.UserId == userId && member.IsActive && !member.IsDeleted &&
                        (member.RoleCode != "Lead" || member.ScopeRootTaskId == null ||
                         task.Id == member.ScopeRootTaskId ||
                         task.ParentTaskId == member.ScopeRootTaskId ||
                         SqlFunc.Subqueryable<ProjectManagementTaskEntity>()
                            .Where(parent1 => parent1.Id == task.ParentTaskId && parent1.TenantId == tenantId && parent1.AppCode == appCode && !parent1.IsDeleted &&
                                (parent1.ParentTaskId == member.ScopeRootTaskId ||
                                 SqlFunc.Subqueryable<ProjectManagementTaskEntity>()
                                    .Where(parent2 => parent2.Id == parent1.ParentTaskId && parent2.TenantId == tenantId && parent2.AppCode == appCode && !parent2.IsDeleted &&
                                        (parent2.ParentTaskId == member.ScopeRootTaskId ||
                                         SqlFunc.Subqueryable<ProjectManagementTaskEntity>()
                                            .Where(parent3 => parent3.Id == parent2.ParentTaskId && parent3.TenantId == tenantId && parent3.AppCode == appCode && !parent3.IsDeleted &&
                                                parent3.ParentTaskId == member.ScopeRootTaskId)
                                            .Any()))
                                    .Any()))
                            .Any()))
                    .Any()) ||
                SqlFunc.Subqueryable<ProjectManagementTaskGrantEntity>()
                    .Where(grant => grant.TaskId == task.Id &&
                        grant.TenantId == tenantId &&
                        grant.AppCode == appCode &&
                        grant.GranteeUserId == userId &&
                        grant.IsActive &&
                        !grant.IsDeleted)
                    .Any())
            .Any());
}
