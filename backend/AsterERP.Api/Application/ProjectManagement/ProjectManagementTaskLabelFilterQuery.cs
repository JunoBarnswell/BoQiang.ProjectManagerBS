using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 将标签筛选协议转换为数据库侧 EXISTS 谓词，供任务视图和项目报表共享。
/// </summary>
public static class ProjectManagementTaskLabelFilterQuery
{
    public static ProjectManagementTaskLabelFilter? Normalize(ProjectManagementTaskLabelFilter? filter)
    {
        if (filter is null || filter.LabelIds is null || filter.LabelIds.Count == 0) return null;
        var labelIds = filter.LabelIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (labelIds.Count != filter.LabelIds.Count) throw new ValidationException("标签筛选条件不能为空且不能重复");
        if (labelIds.Count > 50) throw new ValidationException("一次最多筛选 50 个标签");
        var matchMode = string.IsNullOrWhiteSpace(filter.MatchMode) ? ProjectManagementTaskLabelMatchModes.Any : filter.MatchMode.Trim();
        if (matchMode is not ProjectManagementTaskLabelMatchModes.Any and not ProjectManagementTaskLabelMatchModes.All)
            throw new ValidationException("标签筛选匹配模式仅支持 Any 或 All");
        return new ProjectManagementTaskLabelFilter(labelIds, matchMode);
    }

    public static ISugarQueryable<ProjectManagementTaskEntity> ApplyToTasks(
        ISugarQueryable<ProjectManagementTaskEntity> tasks,
        ProjectManagementTaskLabelFilter? filter,
        string tenantId,
        string appCode)
    {
        if (filter is null) return tasks;
        if (filter.MatchMode == ProjectManagementTaskLabelMatchModes.All)
        {
            foreach (var labelId in filter.LabelIds)
            {
                tasks = tasks.Where(task => SqlFunc.Subqueryable<ProjectManagementTaskLabelEntity>()
                    .Where(link => link.TaskId == task.Id && link.LabelId == labelId && link.TenantId == tenantId && link.AppCode == appCode && !link.IsDeleted)
                    .Any());
            }
            return tasks;
        }

        return tasks.Where(task => SqlFunc.Subqueryable<ProjectManagementTaskLabelEntity>()
            .Where(link => link.TaskId == task.Id && filter.LabelIds.Contains(link.LabelId) && link.TenantId == tenantId && link.AppCode == appCode && !link.IsDeleted)
            .Any());
    }

    public static ISugarQueryable<ProjectManagementProjectEntity> ApplyToProjects(
        ISugarQueryable<ProjectManagementProjectEntity> projects,
        ProjectManagementTaskLabelFilter? filter,
        string tenantId,
        string appCode)
    {
        if (filter is null) return projects;
        if (filter.MatchMode == ProjectManagementTaskLabelMatchModes.All)
        {
            foreach (var labelId in filter.LabelIds)
            {
                projects = projects.Where(project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>()
                    .Where(task => task.ProjectId == project.Id && task.TenantId == tenantId && task.AppCode == appCode && !task.IsDeleted &&
                        SqlFunc.Subqueryable<ProjectManagementTaskLabelEntity>()
                            .Where(link => link.TaskId == task.Id && link.LabelId == labelId && link.TenantId == tenantId && link.AppCode == appCode && !link.IsDeleted)
                            .Any())
                    .Any());
            }
            return projects;
        }

        return projects.Where(project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>()
            .Where(task => task.ProjectId == project.Id && task.TenantId == tenantId && task.AppCode == appCode && !task.IsDeleted &&
                SqlFunc.Subqueryable<ProjectManagementTaskLabelEntity>()
                    .Where(link => link.TaskId == task.Id && filter.LabelIds.Contains(link.LabelId) && link.TenantId == tenantId && link.AppCode == appCode && !link.IsDeleted)
                    .Any())
            .Any());
    }
}
