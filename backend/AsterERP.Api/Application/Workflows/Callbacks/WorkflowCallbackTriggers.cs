namespace AsterERP.Api.Application.Workflows.Callbacks;

public static class WorkflowCallbackTriggers
{
    public const string ProcessStart = "process-start";
    public const string NodeEnter = "node-enter";
    public const string TaskComplete = "task-complete";
    public const string TaskReject = "task-reject";
    public const string TaskReturn = "task-return";
    public const string ProcessCompleted = "process-completed";
    public const string ProcessWithdrawn = "process-withdrawn";
    public const string ProcessTerminated = "process-terminated";
}
