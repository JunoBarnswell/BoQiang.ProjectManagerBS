using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Domain.Common;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementSearchService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser) : IProjectManagementSearchService
{
    private const int MaxLimit = 50;

    public async Task<ProjectManagementSearchResponse> SearchAsync(
        ProjectManagementSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = Required(query.Keyword, "搜索关键字不能为空");
        if (keyword.Length > 200) throw new ValidationException("搜索关键字不能超过 200 个字符");
        var scope = NormalizeScope(query.Scope);
        var limit = Math.Clamp(query.Limit, 1, MaxLimit);
        var pageIndex = Math.Max(query.PageIndex, 1);
        var projectId = NormalizeOptional(query.ProjectId);
        var status = NormalizeOptional(query.Status);
        if (status is not null && status.Length > 64) throw new ValidationException("状态筛选不能超过 64 个字符");
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            throw new ValidationException("搜索时间范围无效");
        RequireTenant();
        RequireApp();

        var db = databaseAccessor.GetProjectManagementDb();
        var projects = new List<ProjectManagementSearchItem>();
        var tasks = new List<ProjectManagementSearchItem>();
        var milestones = new List<ProjectManagementSearchItem>();
        var labels = new List<ProjectManagementSearchItem>();
        var members = new List<ProjectManagementSearchItem>();
        var comments = new List<ProjectManagementSearchItem>();

        if (scope is "all" or "projects")
        {
            var projectQuery = db.Queryable<ProjectManagementProjectEntity>()
                .Where(item => !item.IsDeleted &&
                    (item.ProjectCode.Contains(keyword) || item.ProjectName.Contains(keyword) ||
                     (item.Description != null && item.Description.Contains(keyword))));
            if (projectId is not null) projectQuery = projectQuery.Where(item => item.Id == projectId);
            if (status is not null) projectQuery = projectQuery.Where(item => item.Status == status);
            projectQuery = ApplyTime(projectQuery, query.From, query.To);
            var rows = await projectQuery
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            projects = rows.Select(item => new ProjectManagementSearchItem(
                "project", item.Id, item.Id, item.ProjectName,
                Snippet(item.Description ?? item.ProjectCode, keyword),
                $"/projects/{Segment(item.Id)}/overview", item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        if (scope is "all" or "tasks")
        {
            var taskQuery = db.Queryable<ProjectManagementTaskEntity>()
                .Where(item => !item.IsDeleted &&
                    (item.TaskCode.Contains(keyword) || item.Title.Contains(keyword) ||
                     (item.Summary != null && item.Summary.Contains(keyword)) ||
                     (item.Description != null && item.Description.Contains(keyword))));
            if (projectId is not null) taskQuery = taskQuery.Where(item => item.ProjectId == projectId);
            if (status is not null) taskQuery = taskQuery.Where(item => item.Status == status);
            taskQuery = ApplyTime(taskQuery, query.From, query.To);
            var rows = await taskQuery
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            tasks = rows.Select(item => new ProjectManagementSearchItem(
                "task", item.Id, item.ProjectId, item.Title,
                Snippet(item.Description ?? item.Summary ?? item.TaskCode, keyword),
                $"/projects/{Segment(item.ProjectId)}/tasks?taskId={QueryValue(item.Id)}",
                item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        if (scope is "all" or "milestones")
        {
            var milestoneQuery = db.Queryable<ProjectManagementMilestoneEntity>()
                .Where(item => !item.IsDeleted &&
                    (item.MilestoneName.Contains(keyword) ||
                     (item.Description != null && item.Description.Contains(keyword))));
            if (projectId is not null) milestoneQuery = milestoneQuery.Where(item => item.ProjectId == projectId);
            if (status is not null) milestoneQuery = milestoneQuery.Where(item => item.Status == status);
            milestoneQuery = ApplyTime(milestoneQuery, query.From, query.To);
            var rows = await milestoneQuery
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            milestones = rows.Select(item => new ProjectManagementSearchItem(
                "milestone", item.Id, item.ProjectId, item.MilestoneName,
                Snippet(item.Description ?? item.Status, keyword),
                $"/projects/{Segment(item.ProjectId)}/milestones?milestoneId={QueryValue(item.Id)}",
                item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        if (scope is "all" or "labels")
        {
            var labelQuery = db.Queryable<ProjectManagementLabelEntity>()
                .Where(item => !item.IsDeleted &&
                    (item.LabelName.Contains(keyword) || item.Color.Contains(keyword)));
            if (projectId is not null) labelQuery = labelQuery.Where(item => item.ProjectId == null || item.ProjectId == projectId);
            labelQuery = ApplyTime(labelQuery, query.From, query.To);
            var rows = await labelQuery
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            labels = rows.Select(item => new ProjectManagementSearchItem(
                "label", item.Id, item.ProjectId ?? string.Empty, item.LabelName,
                Snippet(item.Color, keyword),
                item.ProjectId is null
                    ? "/projects"
                    : $"/projects/{Segment(item.ProjectId)}/tasks?labelId={QueryValue(item.Id)}",
                item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        if (scope is "all" or "members")
        {
            var memberQuery = db.Queryable<ProjectManagementProjectMemberEntity>()
                .Where(item => !item.IsDeleted && item.IsActive &&
                    (item.UserId.Contains(keyword) ||
                     (item.EmploymentId != null && item.EmploymentId.Contains(keyword)) ||
                     item.RoleCode.Contains(keyword)));
            if (projectId is not null) memberQuery = memberQuery.Where(item => item.ProjectId == projectId);
            if (status is not null) memberQuery = memberQuery.Where(item => item.RoleCode == status);
            memberQuery = ApplyTime(memberQuery, query.From, query.To);
            var rows = await memberQuery
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            members = rows.Select(item => new ProjectManagementSearchItem(
                "member", item.Id, item.ProjectId, item.UserId,
                Snippet(item.EmploymentId ?? item.RoleCode, keyword),
                $"/projects/{Segment(item.ProjectId)}/members?userId={QueryValue(item.UserId)}",
                item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        if (scope is "all" or "comments")
        {
            var commentQuery = db.Queryable<ProjectManagementTaskCommentEntity>()
                .Where(item => !item.IsDeleted &&
                    (item.Markdown.Contains(keyword) || item.AuthorUserId.Contains(keyword)));
            if (projectId is not null) commentQuery = commentQuery.Where(item => item.ProjectId == projectId);
            if (status is not null)
            {
                commentQuery = commentQuery.Where(item => SqlFunc.Subqueryable<ProjectManagementTaskEntity>()
                    .Where(task => task.Id == item.TaskId && task.Status == status && !task.IsDeleted)
                    .Any());
            }
            if (query.From.HasValue && query.To.HasValue)
            {
                var from = query.From.Value;
                var to = query.To.Value;
                commentQuery = commentQuery.Where(item =>
                    (item.CreatedTime >= from && item.CreatedTime <= to) ||
                    (item.EditedTime.HasValue && item.EditedTime.Value >= from && item.EditedTime.Value <= to));
            }
            else if (query.From.HasValue)
            {
                var from = query.From.Value;
                commentQuery = commentQuery.Where(item => item.CreatedTime >= from || (item.EditedTime.HasValue && item.EditedTime.Value >= from));
            }
            else if (query.To.HasValue)
            {
                var to = query.To.Value;
                commentQuery = commentQuery.Where(item => item.CreatedTime <= to || (item.EditedTime.HasValue && item.EditedTime.Value <= to));
            }
            var rows = await commentQuery
                .OrderBy(item => item.EditedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            comments = rows.Select(item => new ProjectManagementSearchItem(
                "comment", item.Id, item.ProjectId, "任务评论",
                Snippet(item.Markdown, keyword),
                $"/projects/{Segment(item.ProjectId)}/tasks?taskId={QueryValue(item.TaskId)}&commentId={QueryValue(item.Id)}",
                item.EditedTime ?? item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        return new ProjectManagementSearchResponse(projects, tasks, milestones, labels, members, comments);
    }

    private static ISugarQueryable<T> ApplyTime<T>(ISugarQueryable<T> source, DateTime? from, DateTime? to) where T : EntityBase
    {
        if (from.HasValue && to.HasValue)
        {
            var start = from.Value;
            var end = to.Value;
            source = source.Where(item =>
                (item.CreatedTime >= start && item.CreatedTime <= end) ||
                (item.UpdatedTime.HasValue && item.UpdatedTime.Value >= start && item.UpdatedTime.Value <= end));
        }
        else if (from.HasValue)
        {
            var start = from.Value;
            source = source.Where(item => item.CreatedTime >= start || (item.UpdatedTime.HasValue && item.UpdatedTime.Value >= start));
        }
        else if (to.HasValue)
        {
            var end = to.Value;
            source = source.Where(item => item.CreatedTime <= end || (item.UpdatedTime.HasValue && item.UpdatedTime.Value <= end));
        }
        return source;
    }

    private static string NormalizeScope(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "all" => "all",
        "projects" or "project" => "projects",
        "tasks" or "task" => "tasks",
        "milestones" or "milestone" => "milestones",
        "labels" or "label" => "labels",
        "members" or "member" or "people" => "members",
        "comments" or "comment" => "comments",
        _ => throw new ValidationException("搜索范围不受支持")
    };

    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Snippet(string value, string keyword)
    {
        var plainText = value.Replace("\0", string.Empty, StringComparison.Ordinal);
        var index = plainText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return plainText.Length > 180 ? plainText[..180] : plainText;
        var start = Math.Max(0, index - 60);
        var length = Math.Min(plainText.Length - start, 180);
        return (start > 0 ? "…" : string.Empty) + plainText.Substring(start, length) + (start + length < plainText.Length ? "…" : string.Empty);
    }

    private static string Segment(string value) => Uri.EscapeDataString(value);
    private static string QueryValue(string value) => Uri.EscapeDataString(value);
    private string RequireTenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string RequireApp() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
}
