using AsterERP.Api.Modules.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 任务进度的唯一计算入口：只以有效叶子任务作为项目、里程碑和统计的事实来源，
/// 使父节点不会在汇总时与后代重复计入。
/// </summary>
internal static class ProjectManagementTaskProgressCalculator
{
    public static ProjectManagementTaskProgressSnapshot Create(IEnumerable<ProjectManagementTaskEntity> source)
    {
        var allTasks = source.ToList();
        var includedById = allTasks
            .Where(IsIncluded)
            .GroupBy(task => task.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var parentIds = allTasks
            .Select(task => task.ParentTaskId)
            .Where(parentTaskId => !string.IsNullOrWhiteSpace(parentTaskId) && includedById.ContainsKey(parentTaskId))
            .Select(parentTaskId => parentTaskId!)
            .ToHashSet(StringComparer.Ordinal);
        var childrenByParentId = includedById.Values
            .Where(task => !string.IsNullOrWhiteSpace(task.ParentTaskId) && includedById.ContainsKey(task.ParentTaskId))
            .GroupBy(task => task.ParentTaskId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProjectManagementTaskEntity>)group.ToList(), StringComparer.Ordinal);
        var leaves = includedById.Values.Where(task => !parentIds.Contains(task.Id)).ToList();
        var descendantLeaves = new Dictionary<string, IReadOnlyList<ProjectManagementTaskEntity>>(StringComparer.Ordinal);
        var parentProgress = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var parentId in parentIds)
        {
            parentProgress[parentId] = CalculateProgress(ResolveLeafDescendants(parentId, includedById, parentIds, childrenByParentId, descendantLeaves, new HashSet<string>(StringComparer.Ordinal)));
        }

        return new ProjectManagementTaskProgressSnapshot(leaves, parentProgress, CalculateProgress(leaves));
    }

    public static bool IsIncluded(ProjectManagementTaskEntity task) =>
        !task.IsDeleted &&
        !string.Equals(task.Status, ProjectManagementDomainRules.TaskCancelled, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(task.Status, "Canceled", StringComparison.OrdinalIgnoreCase);

    public static bool IsCompleted(ProjectManagementTaskEntity task) =>
        string.Equals(task.Status, ProjectManagementDomainRules.TaskDone, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(task.Status, "Completed", StringComparison.OrdinalIgnoreCase);

    public static decimal CalculateProgress(IEnumerable<ProjectManagementTaskEntity> tasks)
    {
        var weighted = tasks.Select(task => new { Task = task, Weight = task.EstimateMinutes ?? 1 }).ToList();
        var totalWeight = weighted.Sum(item => item.Weight);
        return totalWeight <= 0
            ? 0
            : decimal.Round(weighted.Sum(item => item.Task.ProgressPercent * item.Weight) / totalWeight, 2, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<ProjectManagementTaskEntity> ResolveLeafDescendants(
        string taskId,
        IReadOnlyDictionary<string, ProjectManagementTaskEntity> includedById,
        IReadOnlySet<string> parentIds,
        IReadOnlyDictionary<string, IReadOnlyList<ProjectManagementTaskEntity>> childrenByParentId,
        IDictionary<string, IReadOnlyList<ProjectManagementTaskEntity>> cache,
        ISet<string> path)
    {
        if (cache.TryGetValue(taskId, out var cached)) return cached;
        if (!includedById.TryGetValue(taskId, out var task) || !path.Add(taskId)) return Array.Empty<ProjectManagementTaskEntity>();

        IReadOnlyList<ProjectManagementTaskEntity> result;
        if (!parentIds.Contains(taskId))
        {
            result = [task];
        }
        else if (!childrenByParentId.TryGetValue(taskId, out var children))
        {
            result = Array.Empty<ProjectManagementTaskEntity>();
        }
        else
        {
            result = children.SelectMany(child => ResolveLeafDescendants(child.Id, includedById, parentIds, childrenByParentId, cache, path)).ToList();
        }

        path.Remove(taskId);
        cache[taskId] = result;
        return result;
    }
}

internal sealed record ProjectManagementTaskProgressSnapshot(
    IReadOnlyList<ProjectManagementTaskEntity> Leaves,
    IReadOnlyDictionary<string, decimal> ParentProgressByTaskId,
    decimal ProjectProgressPercent)
{
    public decimal GetMilestoneProgress(string milestoneId) =>
        ProjectManagementTaskProgressCalculator.CalculateProgress(Leaves.Where(task => task.MilestoneId == milestoneId));
}
