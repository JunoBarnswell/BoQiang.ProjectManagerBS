using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>线性时间 CPM 计算器。它只接收已授权、同项目的快照，不拥有数据访问或写入职责。</summary>
public static class ProjectManagementTaskDependencyAnalysisCalculator
{
    private const string FinishToStart = "FinishToStart";

    public static ProjectManagementTaskDependencyAnalysisResponse Calculate(
        IReadOnlyCollection<ProjectManagementTaskDependencyAnalysisTaskInput> sourceTasks,
        IReadOnlyCollection<ProjectManagementTaskDependencyAnalysisLinkInput> sourceLinks,
        IReadOnlyCollection<ProjectManagementTaskDependencyAnalysisMilestoneInput> milestones,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ProjectManagementTaskDependencyAnalysisDiagnostic>();
        var nodes = sourceTasks.OrderBy(item => item.TaskId, StringComparer.Ordinal).ToDictionary(item => item.TaskId, StringComparer.Ordinal);
        var states = new Dictionary<string, NodeState>(StringComparer.Ordinal);
        foreach (var task in nodes.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!task.PlannedStart.HasValue || !task.PlannedFinish.HasValue)
            {
                diagnostics.Add(new("MissingScheduleDate", "Warning", $"任务“{task.Title}”缺少开始或完成日期，不能纳入关键路径计算。", [task.TaskId]));
                continue;
            }
            if (task.PlannedFinish < task.PlannedStart)
            {
                diagnostics.Add(new("InvalidScheduleDate", "Error", $"任务“{task.Title}”的完成日期早于开始日期，不能纳入关键路径计算。", [task.TaskId]));
                continue;
            }
            states[task.TaskId] = new NodeState(task, task.PlannedFinish.Value - task.PlannedStart.Value);
        }

        var validLinks = new List<ProjectManagementTaskDependencyAnalysisLinkInput>();
        var renderableLinks = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var link in sourceLinks.OrderBy(item => item.DependencyId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var typeIsSupported = string.Equals(link.DependencyType, FinishToStart, StringComparison.Ordinal);
            var endpointsExist = nodes.ContainsKey(link.PredecessorTaskId) && nodes.ContainsKey(link.SuccessorTaskId);
            var endpointsSchedulable = states.ContainsKey(link.PredecessorTaskId) && states.ContainsKey(link.SuccessorTaskId);
            renderableLinks[link.DependencyId] = typeIsSupported && endpointsExist;
            if (!typeIsSupported)
            {
                diagnostics.Add(new("UnsupportedDependencyType", "Warning", "仅 Finish-to-Start 依赖可参与甘特关键路径计算。", [link.PredecessorTaskId, link.SuccessorTaskId], link.DependencyId));
                continue;
            }
            if (!endpointsExist)
            {
                diagnostics.Add(new("DeletedOrInaccessibleTask", "Warning", "依赖指向已删除或当前不可见的任务，连线保留为异常提示。", [link.PredecessorTaskId, link.SuccessorTaskId], link.DependencyId));
                continue;
            }
            if (!endpointsSchedulable)
            {
                diagnostics.Add(new("UnschedulableDependency", "Warning", "依赖的任务缺少可用排期，无法计算自动顺延建议。", [link.PredecessorTaskId, link.SuccessorTaskId], link.DependencyId));
                continue;
            }
            validLinks.Add(link);
        }

        var outgoing = states.Keys.ToDictionary(id => id, _ => new List<ProjectManagementTaskDependencyAnalysisLinkInput>(), StringComparer.Ordinal);
        var incoming = states.Keys.ToDictionary(id => id, _ => new List<ProjectManagementTaskDependencyAnalysisLinkInput>(), StringComparer.Ordinal);
        var indegree = states.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (var link in validLinks)
        {
            outgoing[link.PredecessorTaskId].Add(link);
            incoming[link.SuccessorTaskId].Add(link);
            indegree[link.SuccessorTaskId]++;
        }

        var ready = new SortedSet<string>(indegree.Where(item => item.Value == 0).Select(item => item.Key), StringComparer.Ordinal);
        var order = new List<string>(states.Count);
        while (ready.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = ready.Min!;
            ready.Remove(current);
            order.Add(current);
            foreach (var link in outgoing[current].OrderBy(item => item.SuccessorTaskId, StringComparer.Ordinal).ThenBy(item => item.DependencyId, StringComparer.Ordinal))
            {
                if (--indegree[link.SuccessorTaskId] == 0) ready.Add(link.SuccessorTaskId);
            }
        }
        var residual = states.Keys.Except(order, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (residual.Count > 0)
        {
            diagnostics.Add(new("DependencyCycle", "Error", "任务依赖图存在循环或被循环阻塞，相关任务未计算关键路径。", residual));
            foreach (var id in residual) states[id].IsCyclic = true;
        }

        foreach (var id in order)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = states[id];
            var earliestStart = state.Input.PlannedStart!.Value;
            foreach (var link in incoming[id])
            {
                var predecessor = states[link.PredecessorTaskId];
                if (!predecessor.EarliestFinish.HasValue) continue;
                var constraint = predecessor.EarliestFinish.Value.AddMinutes(link.LagMinutes);
                if (constraint > earliestStart) earliestStart = constraint;
            }
            state.EarliestStart = earliestStart;
            state.EarliestFinish = earliestStart + state.Duration;
        }

        var projectFinish = order.Select(id => states[id].EarliestFinish).Where(item => item.HasValue).Select(item => item!.Value).DefaultIfEmpty().Max();
        if (projectFinish != default)
        {
            foreach (var id in order.AsEnumerable().Reverse())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = states[id];
                var latestFinish = projectFinish;
                foreach (var link in outgoing[id])
                {
                    var successor = states[link.SuccessorTaskId];
                    if (!successor.LatestStart.HasValue) continue;
                    var constraint = successor.LatestStart.Value.AddMinutes(-link.LagMinutes);
                    if (constraint < latestFinish) latestFinish = constraint;
                }
                state.LatestFinish = latestFinish;
                state.LatestStart = latestFinish - state.Duration;
                state.TotalFloatMinutes = Math.Max(0, (int)Math.Round((state.LatestStart.Value - state.EarliestStart!.Value).TotalMinutes, MidpointRounding.AwayFromZero));
            }
        }

        var tasks = nodes.Values.OrderBy(item => item.TaskId, StringComparer.Ordinal).Select(task =>
        {
            var state = states.GetValueOrDefault(task.TaskId);
            return new ProjectManagementTaskDependencyAnalysisTask(task.TaskId, task.Title, task.MilestoneId, task.PlannedStart, task.PlannedFinish,
                state?.EarliestStart, state?.EarliestFinish, state?.LatestStart, state?.LatestFinish, state?.TotalFloatMinutes,
                state is { IsCyclic: false, TotalFloatMinutes: 0 }, state is not null && !state.IsCyclic);
        }).ToList();
        var links = sourceLinks.OrderBy(item => item.DependencyId, StringComparer.Ordinal).Select(link => new ProjectManagementTaskDependencyAnalysisLink(
            link.DependencyId, link.PredecessorTaskId, link.SuccessorTaskId, link.DependencyType, link.LagMinutes,
            renderableLinks.GetValueOrDefault(link.DependencyId), IsCriticalLink(link, states))).ToList();
        var milestoneImpacts = BuildMilestoneImpacts(milestones, tasks, cancellationToken);
        return new(tasks, links, milestoneImpacts, diagnostics, projectFinish == default ? null : projectFinish);
    }

    private static IReadOnlyList<ProjectManagementTaskDependencyAnalysisMilestoneImpact> BuildMilestoneImpacts(
        IReadOnlyCollection<ProjectManagementTaskDependencyAnalysisMilestoneInput> milestones,
        IReadOnlyList<ProjectManagementTaskDependencyAnalysisTask> tasks,
        CancellationToken cancellationToken) => milestones.OrderBy(item => item.MilestoneId, StringComparer.Ordinal).Select(milestone =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var linked = tasks.Where(task => task.MilestoneId == milestone.MilestoneId && task.EarliestFinish.HasValue).ToList();
        var forecast = linked.Select(item => item.EarliestFinish!.Value).DefaultIfEmpty().Max();
        var delay = milestone.DueDate.HasValue && forecast != default ? Math.Max(0, (int)Math.Ceiling((forecast - milestone.DueDate.Value).TotalMinutes)) : 0;
        return new ProjectManagementTaskDependencyAnalysisMilestoneImpact(milestone.MilestoneId, milestone.Name, milestone.DueDate,
            forecast == default ? null : forecast, delay, delay > 0, linked.Select(item => item.TaskId).OrderBy(item => item, StringComparer.Ordinal).ToList());
    }).ToList();

    private static bool IsCriticalLink(ProjectManagementTaskDependencyAnalysisLinkInput link, IReadOnlyDictionary<string, NodeState> states)
    {
        if (!string.Equals(link.DependencyType, FinishToStart, StringComparison.Ordinal) || !states.TryGetValue(link.PredecessorTaskId, out var predecessor) || !states.TryGetValue(link.SuccessorTaskId, out var successor)) return false;
        return predecessor.TotalFloatMinutes == 0 && successor.TotalFloatMinutes == 0 && predecessor.EarliestFinish.HasValue && successor.EarliestStart.HasValue && predecessor.EarliestFinish.Value.AddMinutes(link.LagMinutes) == successor.EarliestStart.Value;
    }

    private sealed class NodeState(ProjectManagementTaskDependencyAnalysisTaskInput input, TimeSpan duration)
    {
        public ProjectManagementTaskDependencyAnalysisTaskInput Input { get; } = input;
        public TimeSpan Duration { get; } = duration;
        public bool IsCyclic { get; set; }
        public DateTime? EarliestStart { get; set; }
        public DateTime? EarliestFinish { get; set; }
        public DateTime? LatestStart { get; set; }
        public DateTime? LatestFinish { get; set; }
        public int? TotalFloatMinutes { get; set; }
    }
}

public sealed record ProjectManagementTaskDependencyAnalysisTaskInput(string TaskId, string Title, string? MilestoneId, DateTime? PlannedStart, DateTime? PlannedFinish);
public sealed record ProjectManagementTaskDependencyAnalysisLinkInput(string DependencyId, string PredecessorTaskId, string SuccessorTaskId, string DependencyType, int LagMinutes);
public sealed record ProjectManagementTaskDependencyAnalysisMilestoneInput(string MilestoneId, string Name, DateTime? DueDate);
