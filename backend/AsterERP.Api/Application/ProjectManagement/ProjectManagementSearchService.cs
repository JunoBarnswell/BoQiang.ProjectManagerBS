using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementSearchService(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser) : IProjectManagementSearchService
{
    public async Task<ProjectManagementSearchResponse> SearchAsync(ProjectManagementSearchQuery query, CancellationToken cancellationToken = default)
    {
        var keyword = Required(query.Keyword, "搜索关键字不能为空");
        if (keyword.Length > 200) throw new ValidationException("搜索关键字不能超过 200 个字符");
        var limit = Math.Clamp(query.Limit, 1, 50);
        var scope = query.Scope.Trim().ToLowerInvariant();
        if (scope is not ("all" or "projects" or "tasks" or "comments")) throw new ValidationException("搜索范围不受支持");
        RequireTenant(); RequireApp();
        var db = databaseAccessor.GetProjectManagementDb();
        var projects = new List<ProjectManagementSearchItem>();
        var tasks = new List<ProjectManagementSearchItem>();
        var comments = new List<ProjectManagementSearchItem>();
        if (scope is "all" or "projects")
        {
            projects = (await db.Queryable<ProjectManagementProjectEntity>().Where(item => !item.IsDeleted && (item.ProjectCode.Contains(keyword) || item.ProjectName.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)))).OrderBy(item => item.UpdatedTime, OrderByType.Desc).Take(limit).ToListAsync(cancellationToken)).Select(item => new ProjectManagementSearchItem("project", item.Id, item.Id, item.ProjectName, Snippet(item.Description ?? item.ProjectCode, keyword), $"/projects/{item.Id}/overview", item.UpdatedTime)).ToList();
        }
        if (scope is "all" or "tasks")
        {
            tasks = (await db.Queryable<ProjectManagementTaskEntity>().Where(item => !item.IsDeleted && (item.TaskCode.Contains(keyword) || item.Title.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)))).OrderBy(item => item.UpdatedTime, OrderByType.Desc).Take(limit).ToListAsync(cancellationToken)).Select(item => new ProjectManagementSearchItem("task", item.Id, item.ProjectId, item.Title, Snippet(item.Description ?? item.TaskCode, keyword), $"/projects/{item.ProjectId}/tasks?taskId={item.Id}", item.UpdatedTime)).ToList();
        }
        if (scope is "all" or "comments")
        {
            comments = (await db.Queryable<ProjectManagementTaskCommentEntity>().Where(item => !item.IsDeleted && item.Markdown.Contains(keyword)).OrderBy(item => item.CreatedTime, OrderByType.Desc).Take(limit).ToListAsync(cancellationToken)).Select(item => new ProjectManagementSearchItem("comment", item.Id, item.ProjectId, "任务评论", Snippet(item.Markdown, keyword), $"/projects/{item.ProjectId}/tasks?taskId={item.TaskId}&commentId={item.Id}", item.EditedTime ?? item.UpdatedTime ?? item.CreatedTime)).ToList();
        }
        return new ProjectManagementSearchResponse(projects, tasks, comments);
    }

    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string Snippet(string value, string keyword) { var index = value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase); if (index < 0) return value.Length > 180 ? value[..180] : value; var start = Math.Max(0, index - 60); var length = Math.Min(value.Length - start, 180); return (start > 0 ? "…" : string.Empty) + value.Substring(start, length) + (start + length < value.Length ? "…" : string.Empty); }
    private string RequireTenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private static string RequireApp() => ProjectManagementPlatformScope.AppCode;
}
