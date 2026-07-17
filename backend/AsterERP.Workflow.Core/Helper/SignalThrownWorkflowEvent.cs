using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Helper;

public record SignalThrownWorkflowEvent(
    string SignalName,
    bool ProcessInstanceScope,
    string? ExecutionId,
    string? ProcessInstanceId,
    string? ProcessDefinitionId) : IWorkflowEvent
{
    public WorkflowEventType Type => WorkflowEventType.CUSTOM;
}
