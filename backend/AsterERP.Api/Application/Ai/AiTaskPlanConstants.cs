namespace AsterERP.Api.Application.Ai;

public static class AiTaskPlanConstants
{
    public static readonly string[] PlanStatuses =
    [
        PlanStatus.Draft,
        PlanStatus.PlanReady,
        PlanStatus.Approved,
        PlanStatus.Running,
        PlanStatus.Paused,
        PlanStatus.Completed,
        PlanStatus.PartialCompleted,
        PlanStatus.Blocked,
        PlanStatus.Failed,
        PlanStatus.Cancelled,
        PlanStatus.Archived,
        PlanStatus.ParseFailed
    ];

    public static readonly string[] ItemStatuses =
    [
        ItemStatus.Pending,
        ItemStatus.Ready,
        ItemStatus.InProgress,
        ItemStatus.WaitingUser,
        ItemStatus.Succeeded,
        ItemStatus.Failed,
        ItemStatus.Skipped,
        ItemStatus.Blocked,
        ItemStatus.Cancelled
    ];

    public static readonly string[] Priorities = ["P0", "P1", "P2"];

    public static readonly string[] OwnerTypes =
    [
        OwnerType.User,
        OwnerType.Agent,
        OwnerType.Tool
    ];

    public static readonly string[] TaskTypes =
    [
        TaskType.Design,
        TaskType.Code,
        TaskType.Test,
        TaskType.Review,
        TaskType.Tool,
        TaskType.Manual
    ];

    public static class PlanStatus
    {
        public const string Draft = "Draft";
        public const string PlanReady = "PlanReady";
        public const string Approved = "Approved";
        public const string Running = "Running";
        public const string Paused = "Paused";
        public const string Completed = "Completed";
        public const string PartialCompleted = "PartialCompleted";
        public const string Blocked = "Blocked";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
        public const string Archived = "Archived";
        public const string ParseFailed = "ParseFailed";
    }

    public static class ItemStatus
    {
        public const string Pending = "Pending";
        public const string Ready = "Ready";
        public const string InProgress = "InProgress";
        public const string WaitingUser = "WaitingUser";
        public const string Succeeded = "Succeeded";
        public const string Failed = "Failed";
        public const string Skipped = "Skipped";
        public const string Blocked = "Blocked";
        public const string Cancelled = "Cancelled";
    }

    public static class OwnerType
    {
        public const string User = "User";
        public const string Agent = "Agent";
        public const string Tool = "Tool";
    }

    public static class TaskType
    {
        public const string Design = "Design";
        public const string Code = "Code";
        public const string Test = "Test";
        public const string Review = "Review";
        public const string Tool = "Tool";
        public const string Manual = "Manual";
    }

    public static class Event
    {
        public const string PlanSaved = "plan_saved";
        public const string PlanApproved = "plan_approved";
        public const string PlanUnapproved = "plan_unapproved";
        public const string PlanCompleted = "plan_completed";
        public const string AgentStarted = "agent_started";
        public const string AgentPaused = "agent_paused";
        public const string AgentCompleted = "agent_completed";
        public const string AgentBlocked = "agent_blocked";
        public const string AgentCancelled = "agent_cancelled";
        public const string ExecutionQueueBuilt = "execution_queue_built";
        public const string TaskReady = "task_ready";
        public const string TaskStarted = "task_started";
        public const string TaskDelta = "task_delta";
        public const string TaskCompleted = "task_completed";
        public const string TaskFailed = "task_failed";
        public const string TaskBlocked = "task_blocked";
        public const string TaskSkipped = "task_skipped";
        public const string TaskWaitingUser = "task_waiting_user";
        public const string TaskToolCallStarted = "task_tool_call_started";
        public const string TaskToolCallCompleted = "task_tool_call_completed";
    }
}
