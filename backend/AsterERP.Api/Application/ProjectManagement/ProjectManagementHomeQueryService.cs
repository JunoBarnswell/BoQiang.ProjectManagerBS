using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementHomeQueryService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementDisplayProjectionService displayProjection,
    ProjectManagementHomeHealthProjector healthProjector) : IProjectManagementHomeQueryService
{
    public async Task<ProjectManagementHomeProjectsResponse> QueryProjectsAsync(ProjectManagementHomeQuery query, CancellationToken cancellationToken = default)
    {
        var projects = await LoadProjectsAsync(query, cancellationToken);
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = projects.Count;
        var page = projects.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToArray();
        var items = await ProjectItemsAsync(page, cancellationToken);
        return new(items, total, pageIndex, pageSize, Sequence(page));
    }

    public async Task<ProjectManagementHomeSummaryResponse> QuerySummaryAsync(ProjectManagementHomeQuery query, CancellationToken cancellationToken = default)
    {
        var projects = await LoadProjectsAsync(query with { PageIndex = 1, PageSize = 5000 }, cancellationToken);
        var items = await ProjectItemsAsync(projects, cancellationToken);
        var health = items.GroupBy(item => item.Health, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ProjectManagementHomeHealthSummary(group.Key, group.Count()))
            .ToArray();
        var leads = items.Where(item => !string.IsNullOrWhiteSpace(item.OwnerUserId))
            .GroupBy(item => new { item.OwnerUserId, item.OwnerDisplayName })
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.OwnerDisplayName ?? group.Key.OwnerUserId, StringComparer.Ordinal)
            .Select(group => new ProjectManagementHomeLeadSummary(group.Key.OwnerUserId, group.Key.OwnerDisplayName ?? group.Key.OwnerUserId, group.Count()))
            .ToArray();
        return new(health, leads, items.Count(item => string.IsNullOrWhiteSpace(item.OwnerUserId)), Sequence(projects));
    }

    private async Task<IReadOnlyList<ProjectManagementProjectEntity>> LoadProjectsAsync(ProjectManagementHomeQuery query, CancellationToken cancellationToken)
    {
        RequirePlatformScope();
        var keyword = query.Keyword?.Trim();
        var projectQuery = databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementProjectEntity>()
            .Where(project => query.IncludeArchived || project.Status != ProjectManagementDomainRules.ProjectArchived);
        if (!string.IsNullOrWhiteSpace(keyword))
            projectQuery = projectQuery.Where(project => project.ProjectCode.Contains(keyword) || project.ProjectName.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(query.Status)) projectQuery = projectQuery.Where(project => project.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Priority)) projectQuery = projectQuery.Where(project => project.Priority == query.Priority);
        if (query.TargetDateFrom is not null) projectQuery = projectQuery.Where(project => project.DueDate >= query.TargetDateFrom);
        if (query.TargetDateTo is not null) projectQuery = projectQuery.Where(project => project.DueDate <= query.TargetDateTo);
        if (!string.IsNullOrWhiteSpace(query.LeadUserId)) projectQuery = projectQuery.Where(project => project.OwnerUserId == query.LeadUserId);

        var rows = await projectQuery.ToListAsync(cancellationToken);
        var projected = await ProjectItemsAsync(rows, cancellationToken);
        if (!string.IsNullOrWhiteSpace(query.Health))
            rows = rows.Where(row => projected.Any(item => item.Id == row.Id && item.Health == query.Health)).ToList();
        return Sort(rows, query.SortBy, query.SortDirection);
    }

    private async Task<IReadOnlyList<ProjectManagementHomeProjectItem>> ProjectItemsAsync(IReadOnlyList<ProjectManagementProjectEntity> projects, CancellationToken cancellationToken)
    {
        if (projects.Count == 0) return [];
        var ids = projects.Select(project => project.Id).ToArray();
        var db = databaseAccessor.GetProjectManagementDb();
        var tasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(task => ids.Contains(task.ProjectId) && !task.IsDeleted)
            .Select(task => new TaskProjection(task.ProjectId, task.Status, task.DueDate, task.BlockedReason))
            .ToListAsync(cancellationToken);
        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>()
            .Where(milestone => ids.Contains(milestone.ProjectId) && !milestone.IsDeleted)
            .OrderBy(milestone => milestone.DueDate, OrderByType.Asc)
            .Select(milestone => new MilestoneProjection(milestone.ProjectId, milestone.Id, milestone.MilestoneName))
            .ToListAsync(cancellationToken);
        var users = await displayProjection.ResolveAsync([], [], projects.Select(project => project.OwnerUserId), cancellationToken);
        var now = DateTime.UtcNow;
        return projects.Select(project =>
        {
            var projectTasks = tasks.Where(task => task.ProjectId == project.Id).ToArray();
            var open = projectTasks.Count(task => task.Status != ProjectManagementDomainRules.TaskDone && task.Status != ProjectManagementDomainRules.TaskCancelled);
            var blocked = projectTasks.Count(task => task.Status == ProjectManagementDomainRules.TaskBlocked || !string.IsNullOrWhiteSpace(task.BlockedReason));
            var health = healthProjector.Project(project, open, blocked, now);
            var milestone = milestones.FirstOrDefault(item => item.ProjectId == project.Id);
            var displayName = users.User(project.OwnerUserId);
            return new ProjectManagementHomeProjectItem(
                project.Id, project.ProjectCode, project.ProjectName, project.Status, project.Priority, health,
                project.OwnerUserId, displayName, project.StartDate, project.DueDate, milestone?.Id, milestone?.Name,
                projectTasks.Length, open, projectTasks.Length - open, project.ProgressPercent, project.UpdatedTime, project.VersionNo);
        }).ToArray();
    }

    private static IReadOnlyList<ProjectManagementProjectEntity> Sort(IReadOnlyList<ProjectManagementProjectEntity> rows, string sortBy, string direction)
    {
        var descending = !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedEnumerable<ProjectManagementProjectEntity> ordered = sortBy.ToLowerInvariant() switch
        {
            "name" => rows.OrderBy(row => row.ProjectName, StringComparer.OrdinalIgnoreCase),
            "targetdate" => rows.OrderBy(row => row.DueDate),
            "priority" => rows.OrderByDescending(row => row.Priority),
            _ => rows.OrderBy(row => row.UpdatedTime ?? row.CreatedTime)
        };
        var result = ordered.ThenBy(row => row.ProjectName, StringComparer.OrdinalIgnoreCase).ToArray();
        return descending ? result.Reverse().ToArray() : result;
    }

    private static long Sequence(IEnumerable<ProjectManagementProjectEntity> projects) => projects.Select(project => project.VersionNo).DefaultIfEmpty(0).Max();
    private static void RequirePlatformScope(ICurrentUser currentUser) => ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
    private void RequirePlatformScope() => RequirePlatformScope(currentUser);

    private sealed record TaskProjection(string ProjectId, string Status, DateTime? DueDate, string? BlockedReason);
    private sealed record MilestoneProjection(string ProjectId, string Id, string Name);
}
