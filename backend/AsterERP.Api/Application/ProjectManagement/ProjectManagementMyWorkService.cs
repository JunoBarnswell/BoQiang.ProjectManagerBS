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
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null) : IProjectManagementMyWorkService, ITransientDependency
{
    public async Task<GridPageResult<ProjectManagementMyWorkProjectOption>> QueryProjectOptionsAsync(
        ProjectManagementMyWorkProjectOptionQuery query,
        CancellationToken cancellationToken = default)
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var userId = RequireUserId();
        var keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();
        var projects = databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(project => project.TenantId == tenantId && project.AppCode == appCode && !project.IsDeleted);

        if (!currentUser.IsAsterErpPlatformAdmin() && !currentUser.HasAsterErpPermission("*"))
        {
            projects = projects.Where(project => project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == appCode && member.UserId == userId && member.IsActive && !member.IsDeleted)
                .Any() || SqlFunc.Subqueryable<ProjectManagementTaskGrantEntity>()
                .Where(grant => grant.ProjectId == project.Id && grant.TenantId == tenantId && grant.AppCode == appCode && grant.GranteeUserId == userId && grant.IsActive && !grant.IsDeleted)
                .Any());
        }
        if (keyword is not null)
        {
            projects = projects.Where(project => project.ProjectCode.Contains(keyword) || project.ProjectName.Contains(keyword));
        }

        var total = new RefAsync<int>();
        var items = await projects
            .OrderBy(project => project.ProjectName, OrderByType.Asc)
            .OrderBy(project => project.ProjectCode, OrderByType.Asc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);
        return new GridPageResult<ProjectManagementMyWorkProjectOption>
        {
            Total = total.Value,
            Items = items.Select(project => new ProjectManagementMyWorkProjectOption(project.Id, project.ProjectCode, project.ProjectName)).ToList(),
        };
    }

    public async Task<GridPageResult<ProjectManagementMyWorkItem>> QueryAsync(ProjectManagementMyWorkQuery query, CancellationToken cancellationToken = default)
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        var normalizedQuery = ProjectManagementMyWorkQueryProtocol.Normalize(query);
        var userId = RequireUserId();
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var category = normalizedQuery.Category!;
        var db = databaseAccessor.GetCurrentDb();
        if (!string.IsNullOrWhiteSpace(normalizedQuery.ProjectId))
            await Policy().EnsureCanViewProjectAsync(normalizedQuery.ProjectId, cancellationToken);

        var tasks = db.Queryable<ProjectManagementTaskEntity>().Where(task => task.TenantId == tenantId && task.AppCode == appCode && !task.IsDeleted);
        if (!currentUser.IsAsterErpPlatformAdmin() && !currentUser.HasAsterErpPermission("*"))
        {
            tasks = tasks.Where(task => SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                .Where(project => project.Id == task.ProjectId && project.TenantId == tenantId && project.AppCode == appCode && !project.IsDeleted &&
                    (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                        .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == appCode && member.UserId == userId && member.IsActive && !member.IsDeleted).Any() ||
                     SqlFunc.Subqueryable<ProjectManagementTaskGrantEntity>()
                        .Where(grant => grant.TaskId == task.Id && grant.TenantId == tenantId && grant.AppCode == appCode && grant.GranteeUserId == userId && grant.IsActive && !grant.IsDeleted).Any())).Any());
        }
        if (!string.IsNullOrWhiteSpace(normalizedQuery.ProjectId)) tasks = tasks.Where(task => task.ProjectId == normalizedQuery.ProjectId);
        if (!normalizedQuery.IncludeCompleted) tasks = tasks.Where(task => task.Status != ProjectManagementDomainRules.TaskDone && task.Status != ProjectManagementDomainRules.TaskCancelled);

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var nextWeek = today.AddDays(7);
        tasks = category switch
        {
            "assigned" => tasks.Where(task => task.AssigneeUserId == userId),
            "participating" => tasks.Where(task => SqlFunc.Subqueryable<ProjectManagementTaskParticipantEntity>()
                .Where(participant => participant.TaskId == task.Id && participant.TenantId == tenantId && participant.AppCode == appCode && participant.UserId == userId && !participant.IsDeleted).Any()),
            "created" => tasks.Where(task => task.CreatedBy == userId),
            "mentioned" => tasks.Where(task => SqlFunc.Subqueryable<ProjectManagementTaskCommentEntity>()
                .Where(comment => comment.TaskId == task.Id && comment.TenantId == tenantId && comment.AppCode == appCode && !comment.IsDeleted && comment.MentionUserIdsJson != null && comment.MentionUserIdsJson.Contains($"\"{userId}\"")).Any()),
            _ => ApplyCategory(tasks, category, userId, tenantId, appCode, today, tomorrow, nextWeek)
        };

        var total = new RefAsync<int>();
        var ordered = Order(tasks, normalizedQuery.SortBy, normalizedQuery.SortDirection);
        var page = await ordered.ToPageListAsync(normalizedQuery.PageIndex, normalizedQuery.PageSize, total, cancellationToken);
        if (page.Count == 0) return new GridPageResult<ProjectManagementMyWorkItem> { Total = total.Value };

        var taskIds = page.Select(task => task.Id).ToList();
        var projectIds = page.Select(task => task.ProjectId).Distinct(StringComparer.Ordinal).ToList();
        var projects = await db.Queryable<ProjectManagementProjectEntity>().Where(project => projectIds.Contains(project.Id) && project.TenantId == tenantId && project.AppCode == appCode && !project.IsDeleted).ToListAsync(cancellationToken);
        var participants = await db.Queryable<ProjectManagementTaskParticipantEntity>().Where(participant => taskIds.Contains(participant.TaskId) && participant.TenantId == tenantId && participant.AppCode == appCode && participant.UserId == userId && !participant.IsDeleted).ToListAsync(cancellationToken);
        var mentions = await db.Queryable<ProjectManagementTaskCommentEntity>().Where(comment => taskIds.Contains(comment.TaskId) && comment.TenantId == tenantId && comment.AppCode == appCode && !comment.IsDeleted && comment.MentionUserIdsJson != null && comment.MentionUserIdsJson.Contains($"\"{userId}\"")).ToListAsync(cancellationToken);
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

    private static ISugarQueryable<ProjectManagementTaskEntity> ApplyCategory(
        ISugarQueryable<ProjectManagementTaskEntity> tasks,
        string category,
        string userId,
        string tenantId,
        string appCode,
        DateTime today,
        DateTime tomorrow,
        DateTime nextWeek)
    {
        tasks = tasks.Where(task => task.AssigneeUserId == userId || task.CreatedBy == userId ||
            SqlFunc.Subqueryable<ProjectManagementTaskParticipantEntity>().Where(participant => participant.TaskId == task.Id && participant.TenantId == tenantId && participant.AppCode == appCode && participant.UserId == userId && !participant.IsDeleted).Any() ||
            SqlFunc.Subqueryable<ProjectManagementTaskCommentEntity>().Where(comment => comment.TaskId == task.Id && comment.TenantId == tenantId && comment.AppCode == appCode && !comment.IsDeleted && comment.MentionUserIdsJson != null && comment.MentionUserIdsJson.Contains($"\"{userId}\"")).Any() ||
            SqlFunc.Subqueryable<ProjectManagementTaskGrantEntity>().Where(grant => grant.TaskId == task.Id && grant.TenantId == tenantId && grant.AppCode == appCode && grant.GranteeUserId == userId && grant.IsActive && !grant.IsDeleted).Any());
        return category switch
        {
            "today" => tasks.Where(task => task.DueDate >= today && task.DueDate < tomorrow),
            "upcoming" => tasks.Where(task => task.DueDate >= tomorrow && task.DueDate < nextWeek),
            "overdue" => tasks.Where(task => task.DueDate != null && task.DueDate < today && task.Status != ProjectManagementDomainRules.TaskDone && task.Status != ProjectManagementDomainRules.TaskCancelled),
            "blocked" => tasks.Where(task => task.Status == ProjectManagementDomainRules.TaskBlocked || (task.BlockedReason != null && task.BlockedReason != "")),
            _ => tasks
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

    private static ProjectManagementTaskResponse Map(ProjectManagementTaskEntity task) => new(task.Id, task.ProjectId, task.MilestoneId, task.ParentTaskId, task.TaskCode, task.Title, task.Description, task.Status, task.Priority, task.AssigneeUserId, task.AssigneeEmploymentId, task.StartDate, task.DueDate, task.ProgressPercent, task.Weight, task.SortOrder, task.Depth, task.VersionNo, task.CreatedTime, task.UpdatedTime, 0, task.Status != ProjectManagementDomainRules.TaskBlocked, task.BlockedReason);

    private ProjectManagementAccessPolicy Policy() => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);

    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
}
