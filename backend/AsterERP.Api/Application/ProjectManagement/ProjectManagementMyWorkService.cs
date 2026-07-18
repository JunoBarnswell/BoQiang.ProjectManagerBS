using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementMyWorkService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser) : IProjectManagementMyWorkService, ITransientDependency
{
    private static readonly string[] Categories = ["all", "assigned", "participating", "created", "mentioned", "today", "upcoming", "overdue", "blocked"];

    public async Task<GridPageResult<ProjectManagementMyWorkItem>> QueryAsync(ProjectManagementMyWorkQuery query, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var category = NormalizeCategory(query.Category);
        var db = databaseAccessor.GetCurrentDb();
        var tasks = db.Queryable<ProjectManagementTaskEntity>().Where(task => !task.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.ProjectId)) tasks = tasks.Where(task => task.ProjectId == query.ProjectId.Trim());
        if (!query.IncludeCompleted) tasks = tasks.Where(task => task.Status != "Completed" && task.Status != "Cancelled");

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var nextWeek = today.AddDays(7);
        tasks = category switch
        {
            "assigned" => tasks.Where(task => task.AssigneeUserId == userId),
            "participating" => tasks.Where(task => SqlFunc.Subqueryable<ProjectManagementTaskParticipantEntity>()
                .Where(participant => participant.TaskId == task.Id && participant.UserId == userId && !participant.IsDeleted).Any()),
            "created" => tasks.Where(task => task.CreatedBy == userId),
            "mentioned" => tasks.Where(task => SqlFunc.Subqueryable<ProjectManagementTaskCommentEntity>()
                .Where(comment => comment.TaskId == task.Id && !comment.IsDeleted && comment.MentionUserIdsJson != null && comment.MentionUserIdsJson.Contains($"\"{userId}\"")).Any()),
            "today" => tasks.Where(task => task.DueDate >= today && task.DueDate < tomorrow),
            "upcoming" => tasks.Where(task => task.DueDate >= tomorrow && task.DueDate < nextWeek),
            "overdue" => tasks.Where(task => task.DueDate != null && task.DueDate < today),
            "blocked" => tasks.Where(task => task.Status == "Blocked" || (task.BlockedReason != null && task.BlockedReason != "")),
            _ => tasks.Where(task => task.AssigneeUserId == userId || task.CreatedBy == userId ||
                SqlFunc.Subqueryable<ProjectManagementTaskParticipantEntity>().Where(participant => participant.TaskId == task.Id && participant.UserId == userId && !participant.IsDeleted).Any() ||
                SqlFunc.Subqueryable<ProjectManagementTaskCommentEntity>().Where(comment => comment.TaskId == task.Id && !comment.IsDeleted && comment.MentionUserIdsJson != null && comment.MentionUserIdsJson.Contains($"\"{userId}\"")).Any())
        };

        var total = new RefAsync<int>();
        var ordered = Order(tasks, query.SortBy, query.SortDirection);
        var page = await ordered.ToPageListAsync(Math.Max(1, query.PageIndex), Math.Clamp(query.PageSize, 1, 200), total, cancellationToken);
        if (page.Count == 0) return new GridPageResult<ProjectManagementMyWorkItem> { Total = total.Value };

        var taskIds = page.Select(task => task.Id).ToList();
        var projectIds = page.Select(task => task.ProjectId).Distinct(StringComparer.Ordinal).ToList();
        var projects = await db.Queryable<ProjectManagementProjectEntity>().Where(project => projectIds.Contains(project.Id) && !project.IsDeleted).ToListAsync(cancellationToken);
        var participants = await db.Queryable<ProjectManagementTaskParticipantEntity>().Where(participant => taskIds.Contains(participant.TaskId) && participant.UserId == userId && !participant.IsDeleted).ToListAsync(cancellationToken);
        var mentions = await db.Queryable<ProjectManagementTaskCommentEntity>().Where(comment => taskIds.Contains(comment.TaskId) && !comment.IsDeleted && comment.MentionUserIdsJson != null && comment.MentionUserIdsJson.Contains($"\"{userId}\"")).ToListAsync(cancellationToken);
        var names = projects.ToDictionary(project => project.Id, project => project.ProjectName, StringComparer.Ordinal);
        var participatingTaskIds = participants.Select(participant => participant.TaskId).ToHashSet(StringComparer.Ordinal);
        var mentionedTaskIds = mentions.Select(comment => comment.TaskId).ToHashSet(StringComparer.Ordinal);

        return new GridPageResult<ProjectManagementMyWorkItem>
        {
            Total = total.Value,
            Items = page.Select(task => new ProjectManagementMyWorkItem(
                Map(task), names.GetValueOrDefault(task.ProjectId, task.ProjectId), task.AssigneeUserId == userId,
                participatingTaskIds.Contains(task.Id), task.CreatedBy == userId, mentionedTaskIds.Contains(task.Id))).ToList()
        };
    }

    private static ISugarQueryable<ProjectManagementTaskEntity> Order(ISugarQueryable<ProjectManagementTaskEntity> tasks, string? sortBy, string? direction)
    {
        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "updated" => tasks.OrderBy(task => task.UpdatedTime, descending ? OrderByType.Desc : OrderByType.Asc),
            "created" => tasks.OrderBy(task => task.CreatedTime, descending ? OrderByType.Desc : OrderByType.Asc),
            "priority" => tasks.OrderBy(task => task.Priority, descending ? OrderByType.Desc : OrderByType.Asc),
            _ => tasks.OrderBy(task => task.DueDate, descending ? OrderByType.Desc : OrderByType.Asc).OrderBy(task => task.UpdatedTime, OrderByType.Desc)
        };
    }

    private static ProjectManagementTaskResponse Map(ProjectManagementTaskEntity task) => new(task.Id, task.ProjectId, task.MilestoneId, task.ParentTaskId, task.TaskCode, task.Title, task.Description, task.Status, task.Priority, task.AssigneeUserId, task.AssigneeEmploymentId, task.StartDate, task.DueDate, task.ProgressPercent, task.Weight, task.EstimateMinutes, task.ActualMinutes, task.SortOrder, task.Depth, task.VersionNo, task.CreatedTime, task.UpdatedTime, 0, task.Status != "Blocked", task.BlockedReason);

    private static string NormalizeCategory(string? category)
    {
        var normalized = string.IsNullOrWhiteSpace(category) ? "all" : category.Trim().ToLowerInvariant();
        if (!Categories.Contains(normalized, StringComparer.Ordinal)) throw new ValidationException("不支持的我的工作分类");
        return normalized;
    }

    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
}
