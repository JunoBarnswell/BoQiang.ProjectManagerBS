using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 仅合并已由数据库聚合后的负责人任务与人员日志结果。这里绝不枚举任务或日志明细，
/// 以保证工作量查询不会退化为按人员查询的 N+1。
/// </summary>
internal static class ProjectManagementTaskWorkloadProjection
{
    public static IReadOnlyList<ProjectManagementTaskWorkloadResponse> Create(
        IEnumerable<ProjectManagementTaskWorkloadTaskAggregate> taskAggregates,
        IEnumerable<ProjectManagementTaskWorkloadLogAggregate> logAggregates)
    {
        var rows = taskAggregates.ToDictionary(
            item => item.UserId,
            item => new MutableWorkload(item),
            StringComparer.Ordinal);

        foreach (var log in logAggregates)
        {
            if (!rows.TryGetValue(log.UserId, out var row))
            {
                row = new MutableWorkload(log.UserId);
                rows.Add(log.UserId, row);
            }

            row.LoggedMinutes = log.LoggedMinutes;
        }

        return rows.Values
            .OrderByDescending(item => item.LoggedMinutes)
            .ThenByDescending(item => item.EstimatedMinutes)
            .ThenBy(item => item.UserId, StringComparer.Ordinal)
            .Select(item => new ProjectManagementTaskWorkloadResponse(
                item.UserId,
                item.TodoTaskCount,
                item.InProgressTaskCount,
                item.CompletedTaskCount,
                item.OverdueTaskCount,
                item.EstimatedMinutes,
                item.LoggedMinutes))
            .ToList();
    }

    private sealed class MutableWorkload
    {
        public MutableWorkload(ProjectManagementTaskWorkloadTaskAggregate source)
        {
            UserId = source.UserId;
            TodoTaskCount = source.TodoTaskCount;
            InProgressTaskCount = source.InProgressTaskCount;
            CompletedTaskCount = source.CompletedTaskCount;
            OverdueTaskCount = source.OverdueTaskCount;
            EstimatedMinutes = source.EstimatedMinutes;
        }

        public MutableWorkload(string userId) => UserId = userId;

        public string UserId { get; }
        public int TodoTaskCount { get; }
        public int InProgressTaskCount { get; }
        public int CompletedTaskCount { get; }
        public int OverdueTaskCount { get; }
        public int EstimatedMinutes { get; }
        public int LoggedMinutes { get; set; }
    }
}

internal sealed class ProjectManagementTaskWorkloadTaskAggregate
{
    public string UserId { get; init; } = string.Empty;
    public int TodoTaskCount { get; init; }
    public int InProgressTaskCount { get; init; }
    public int CompletedTaskCount { get; init; }
    public int OverdueTaskCount { get; init; }
    public int EstimatedMinutes { get; init; }
}

internal sealed class ProjectManagementTaskWorkloadLogAggregate
{
    public string UserId { get; init; } = string.Empty;
    public int LoggedMinutes { get; init; }
}
