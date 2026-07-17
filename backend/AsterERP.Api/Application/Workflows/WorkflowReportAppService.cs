using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Contracts.Workflows;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowReportAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IClock clock,
    IWorkflowTaskAppService taskService) : IWorkflowReportAppService
{
    public async Task<WorkflowReportOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        var instances = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.AppCode == appCode)
            .ToListAsync(cancellationToken);
        var summary = await taskService.GetSummaryAsync(cancellationToken);
        var overdueCount = await databaseAccessor.GetCurrentDb().Queryable<TaskEntity>()
            .Where(item => item.DueDate != null && item.DueDate < clock.Now)
            .CountAsync(cancellationToken);

        var completedDurations = instances
            .Where(item => item.FinishedAt is not null)
            .Select(item => (item.FinishedAt!.Value - item.StartedAt).TotalHours)
            .Where(item => item >= 0)
            .ToList();

        var bottlenecks = await BuildBottleneckNodesAsync(cancellationToken);
        return new WorkflowReportOverviewResponse(
            new WorkflowApprovalStatisticsResponse(
                instances.Count,
                instances.Count(item => string.Equals(item.Status, "Running", StringComparison.OrdinalIgnoreCase)),
                instances.Count(item => string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase)),
                instances.Count(item => string.Equals(item.Status, "Rejected", StringComparison.OrdinalIgnoreCase)),
                instances.Count(item => string.Equals(item.Status, "Withdrawn", StringComparison.OrdinalIgnoreCase)),
                instances.Count(item => string.Equals(item.Status, "Terminated", StringComparison.OrdinalIgnoreCase)),
                summary.Todo,
                summary.Done,
                summary.Cc),
            new WorkflowEfficiencyAnalysisResponse(
                completedDurations.Count == 0 ? 0 : Math.Round(completedDurations.Average(), 2),
                overdueCount,
                bottlenecks),
            instances
                .GroupBy(item => item.BusinessType, StringComparer.OrdinalIgnoreCase)
                .Select(group => new WorkflowBusinessDataReportItemResponse(
                    group.Key,
                    group.Count(),
                    group.Count(item => string.Equals(item.Status, "Running", StringComparison.OrdinalIgnoreCase)),
                    group.Count(item => item.FinishedAt is not null)))
                .OrderByDescending(item => item.Total)
                .ToList());
    }

    private async Task<IReadOnlyList<WorkflowBottleneckNodeResponse>> BuildBottleneckNodesAsync(CancellationToken cancellationToken)
    {
        var tasks = await databaseAccessor.GetCurrentDb().Queryable<HistoricTaskInstanceEntity>()
            .Where(item => item.EndTime != null && item.StartTime != null)
            .OrderBy(item => item.EndTime, OrderByType.Desc)
            .Take(500)
            .ToListAsync(cancellationToken);

        return tasks
            .GroupBy(item => item.TaskDefinitionKey ?? item.Name ?? item.Id)
            .Select(group =>
            {
                var durations = group
                    .Select(item => (item.EndTime!.Value - item.StartTime!.Value).TotalHours)
                    .Where(item => item >= 0)
                    .ToList();
                return new WorkflowBottleneckNodeResponse(
                    group.Key,
                    group.FirstOrDefault()?.Name ?? group.Key,
                    group.Count(),
                    durations.Count == 0 ? 0 : Math.Round(durations.Average(), 2));
            })
            .OrderByDescending(item => item.AverageDurationHours)
            .ThenByDescending(item => item.CompletedCount)
            .Take(10)
            .ToList();
    }
}

