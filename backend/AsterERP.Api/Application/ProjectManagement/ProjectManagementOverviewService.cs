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

public sealed class ProjectManagementOverviewService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy,
    ProjectManagementHomeHealthProjector? healthProjector = null,
    IProjectManagementDisplayProjectionService? displayProjection = null) : IProjectManagementOverviewService, ITransientDependency
{
    public async Task<GridPageResult<ProjectManagementOverviewItem>> QueryAsync(ProjectManagementOverviewQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var db = databaseAccessor.GetCurrentDb();
        if (!string.IsNullOrWhiteSpace(query.ProjectId))
            await accessPolicy.EnsureCanViewProjectAsync(query.ProjectId, cancellationToken);

        var userId = currentUser.GetAsterErpUserId()?.Trim();
        var projects = db.Queryable<ProjectManagementProjectEntity>()
            .Where(project => !project.IsDeleted && project.TenantId == tenantId && project.AppCode == appCode);
        if (!currentUser.IsAsterErpPlatformAdmin() && !currentUser.HasAsterErpPermission("*"))
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
            projects = projects.Where(project => project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == appCode && member.UserId == userId && member.IsActive && !member.IsDeleted).Any());
        }
        if (!string.IsNullOrWhiteSpace(query.ProjectId)) projects = projects.Where(project => project.Id == query.ProjectId);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            projects = projects.Where(project => project.ProjectCode.Contains(keyword) || project.ProjectName.Contains(keyword));
        }

        var total = new RefAsync<int>();
        var page = await projects.OrderBy(project => project.UpdatedTime, OrderByType.Desc).OrderBy(project => project.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);
        var ids = page.Select(project => project.Id).ToList();
        if (ids.Count == 0) return new GridPageResult<ProjectManagementOverviewItem> { Total = total.Value };

        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(task => ids.Contains(task.ProjectId) && task.TenantId == tenantId && task.AppCode == appCode && !task.IsDeleted).ToListAsync(cancellationToken);
        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>().Where(milestone => ids.Contains(milestone.ProjectId) && milestone.TenantId == tenantId && milestone.AppCode == appCode && !milestone.IsDeleted)
            .OrderBy(milestone => milestone.DueDate, OrderByType.Asc).ToListAsync(cancellationToken);
        var members = await db.Queryable<ProjectManagementProjectMemberEntity>().Where(member => ids.Contains(member.ProjectId) && member.TenantId == tenantId && member.AppCode == appCode && member.IsActive && !member.IsDeleted).ToListAsync(cancellationToken);
        var displays = await DisplayProjection.ResolveAsync([], [], page.Select(project => project.OwnerUserId).Concat(tasks.Select(task => task.AssigneeUserId)), cancellationToken);
        var now = DateTime.UtcNow;
        var projector = healthProjector ?? new ProjectManagementHomeHealthProjector();
        return new GridPageResult<ProjectManagementOverviewItem>
        {
            Total = total.Value,
            Items = page.Select(project =>
            {
                var snapshot = ProjectManagementTaskProgressCalculator.Create(tasks.Where(task => task.ProjectId == project.Id));
                var leaves = snapshot.Leaves;
                var overdue = leaves.Where(task => task.DueDate.HasValue && task.DueDate.Value < now && !ProjectManagementTaskProgressCalculator.IsCompleted(task)).ToList();
                var overdueIds = overdue.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
                var inProgressCount = leaves.Count(task => task.Status == ProjectManagementDomainRules.TaskInProgress);
                var blockedCount = leaves.Count(task => task.Status == ProjectManagementDomainRules.TaskBlocked);
                var openCount = leaves.Count(task => !ProjectManagementTaskProgressCalculator.IsCompleted(task));
                var dueSoonCount = leaves.Count(task => task.DueDate.HasValue && task.DueDate.Value >= now && task.DueDate.Value <= now.AddDays(7) && !ProjectManagementTaskProgressCalculator.IsCompleted(task));
                var wipExceededBy = project.WipLimit.HasValue ? Math.Max(0, inProgressCount - project.WipLimit.Value) : 0;
                var memberCapacities = members.Where(member => member.ProjectId == project.Id).ToDictionary(member => member.UserId, member => Math.Max(1, member.SuggestedCapacityMinutes), StringComparer.Ordinal);
                var people = leaves.Where(task => !string.IsNullOrWhiteSpace(task.AssigneeUserId)).GroupBy(task => task.AssigneeUserId!, StringComparer.Ordinal)
                    .Select(group =>
                    {
                        var estimatedMinutes = group.Where(task => !ProjectManagementTaskProgressCalculator.IsCompleted(task)).Sum(task => task.EstimateMinutes ?? 0);
                        var capacityMinutes = memberCapacities.GetValueOrDefault(group.Key, 2400);
                        return new ProjectManagementOverviewPersonSummary(group.Key, group.Count(), group.Count(ProjectManagementTaskProgressCalculator.IsCompleted), group.Count(task => overdueIds.Contains(task.Id)), displays.User(group.Key), estimatedMinutes, capacityMinutes, Math.Round(Math.Min(100m, estimatedMinutes * 100m / Math.Max(1, capacityMinutes)), 1));
                    })
                    .OrderByDescending(item => item.TaskCount).Take(10).ToList();
                var workItemTypeDistribution = CreateDistribution(leaves.Select(task => string.IsNullOrWhiteSpace(task.WorkItemType) ? "Task" : task.WorkItemType));
                var statusDistribution = CreateDistribution(leaves.Select(task => task.Status));
                var requirementTypeDistribution = CreateDistribution(leaves.Select(task => string.IsNullOrWhiteSpace(task.RequirementType) ? "未分类" : task.RequirementType));
                var pendingCount = leaves.Count(task => task.Status == ProjectManagementDomainRules.TaskTodo);
                var storyPointsTotal = leaves.Sum(task => task.StoryPoints ?? 0);
                return new ProjectManagementOverviewItem(
                    new ProjectManagementProjectResponse(project.Id, project.TenantId, project.AppCode, project.ProjectCode, project.ProjectName, project.Description, project.Status, project.Priority, project.OwnerUserId, project.StartDate, project.DueDate, project.CompletedAt, project.WipLimit, project.ProgressPercent, project.VersionNo, project.CreatedTime, project.UpdatedTime, displays.User(project.OwnerUserId)),
                    leaves.Count, leaves.Count(ProjectManagementTaskProgressCalculator.IsCompleted), inProgressCount, overdue.Count, blockedCount, snapshot.ProjectProgressPercent,
                    milestones.Count(item => item.ProjectId == project.Id), members.Count(item => item.ProjectId == project.Id),
                    milestones.Where(item => item.ProjectId == project.Id).Take(10).Select(item => new ProjectManagementOverviewMilestoneSummary(item.Id, item.MilestoneName, item.Status, item.HealthStatus, item.ProgressPercent, item.DueDate)).ToList(), people,
                    new ProjectManagementProjectRiskSummary(overdue.Count, blockedCount, dueSoonCount, inProgressCount, project.WipLimit, wipExceededBy > 0, wipExceededBy, overdue.Count > 0 || blockedCount > 0 || dueSoonCount > 0),
                    projector.Project(project, openCount, blockedCount, now), workItemTypeDistribution, statusDistribution, pendingCount, storyPointsTotal, requirementTypeDistribution);
            }).ToList()
        };
    }

    private IProjectManagementDisplayProjectionService DisplayProjection => displayProjection ?? new ProjectManagementDisplayProjectionService(databaseAccessor);

    private static IReadOnlyList<ProjectManagementOverviewDistribution> CreateDistribution(IEnumerable<string> values)
    {
        var materialized = values.ToList();
        if (materialized.Count == 0) return [];
        return materialized.GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProjectManagementOverviewDistribution(group.Key, group.Count(), Math.Round(group.Count() * 100m / materialized.Count, 1)))
            .OrderByDescending(item => item.Count)
            .ToList();
    }

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
}
