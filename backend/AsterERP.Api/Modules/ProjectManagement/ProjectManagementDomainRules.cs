using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Modules.ProjectManagement;

/// <summary>
/// ProjectManagement 的唯一领域语义入口。Application 只负责装配工作区、持久化和外部能力，
/// 不得在各服务中重新定义状态、角色、依赖类型或边界校验。
/// </summary>
public static class ProjectManagementDomainRules
{
    /// <summary>任务树的默认总层数；根任务为第 1 层。</summary>
    public const int DefaultTaskHierarchyMaxDepth = 5;
    public const string ProjectPlanning = "Planning";
    public const string ProjectActive = "Active";
    public const string ProjectPaused = "Paused";
    public const string ProjectCompleted = "Completed";
    public const string ProjectCanceled = "Canceled";
    public const string ProjectArchived = "Archived";

    public const string TaskTodo = "Todo";
    public const string TaskInProgress = "InProgress";
    public const string TaskBlocked = "Blocked";
    public const string TaskDone = "Done";
    public const string TaskCancelled = "Cancelled";

    public const string MilestonePlanned = "Planned";
    public const string MilestoneActive = "Active";
    public const string MilestoneCompleted = "Completed";
    public const string MilestoneArchived = "Archived";

    public static readonly IReadOnlySet<string> ProjectRoles = new HashSet<string>(["Owner", "Manager", "Lead", "Member", "Viewer"], StringComparer.Ordinal);
    public static readonly IReadOnlySet<string> TaskStatuses = new HashSet<string>([TaskTodo, TaskInProgress, TaskBlocked, TaskDone, TaskCancelled], StringComparer.Ordinal);
    public static readonly IReadOnlySet<string> DependencyTypes = new HashSet<string>(["FinishToStart", "StartToStart", "FinishToFinish", "StartToFinish"], StringComparer.Ordinal);

    public static string RequireProjectStatus(string value) => RequireAllowed(value, [ProjectPlanning, ProjectActive, ProjectPaused, ProjectCompleted, ProjectCanceled, ProjectArchived], "项目状态不受支持");
    public static string RequireTaskStatus(string value) => RequireAllowed(value, TaskStatuses, "任务状态不受支持");
    public static string RequireMilestoneStatus(string value) => RequireAllowed(value, [MilestonePlanned, MilestoneActive, MilestoneCompleted, MilestoneArchived], "里程碑状态不受支持");
    public static string RequireRole(string value) => RequireAllowed(value, ProjectRoles, "成员角色不受支持");
    public static string RequireDependencyType(string value) => RequireAllowed(value, DependencyTypes, "依赖类型不受支持");

    public static void EnsureProjectStatusTransition(string current, string next)
    {
        if (current == next) return;
        var allowed = current switch
        {
            ProjectPlanning => new[] { ProjectActive, ProjectCanceled, ProjectArchived },
            ProjectActive => new[] { ProjectPaused, ProjectCompleted, ProjectCanceled },
            ProjectPaused => new[] { ProjectActive, ProjectCanceled },
            ProjectCompleted => new[] { ProjectArchived },
            ProjectCanceled => new[] { ProjectArchived },
            _ => Array.Empty<string>()
        };
        if (!allowed.Contains(next, StringComparer.Ordinal)) throw new ValidationException($"项目状态不能从 {current} 变更为 {next}");
    }

    public static void EnsureTaskStatusTransition(string current, string next)
    {
        if (current == next) return;
        var allowed = current switch
        {
            TaskTodo => new[] { TaskInProgress, TaskCancelled },
            TaskInProgress => new[] { TaskBlocked, TaskDone, TaskCancelled },
            TaskBlocked => new[] { TaskTodo, TaskInProgress, TaskCancelled },
            _ => Array.Empty<string>()
        };
        if (!allowed.Contains(next, StringComparer.Ordinal)) throw new ValidationException($"任务状态不能从 {current} 变更为 {next}");
    }

    public static void EnsureMilestoneStatusTransition(string current, string next)
    {
        if (current == next) return;
        var allowed = current switch
        {
            MilestonePlanned => new[] { MilestoneActive, MilestoneArchived },
            MilestoneActive => new[] { MilestoneCompleted, MilestoneArchived },
            MilestoneCompleted => new[] { MilestoneArchived },
            _ => Array.Empty<string>()
        };
        if (!allowed.Contains(next, StringComparer.Ordinal)) throw new ValidationException($"里程碑状态不能从 {current} 变更为 {next}");
    }

    public static void ValidateDates(DateTime? startDate, DateTime? dueDate, string subject)
    {
        if (startDate.HasValue && dueDate.HasValue && dueDate < startDate) throw new ValidationException($"{subject}结束日期不能早于开始日期");
    }

    public static decimal RequireProgress(decimal progress, string subject) => progress is < 0 or > 100 ? throw new ValidationException($"{subject}进度必须在 0 到 100 之间") : progress;

    public static void EnsureTaskDepth(int depth)
    {
        if (depth < 0 || depth >= DefaultTaskHierarchyMaxDepth) throw new ValidationException($"任务层级不能超过 {DefaultTaskHierarchyMaxDepth} 层");
    }

    private static string RequireAllowed(string value, IEnumerable<string> allowed, string message)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return allowed.Contains(normalized, StringComparer.Ordinal) ? normalized : throw new ValidationException(message);
    }
}
