using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Helper;

public record MessageThrownWorkflowEvent(
    string MessageName,
    string? ExecutionId,
    string? ProcessInstanceId,
    string? ProcessDefinitionId) : IWorkflowEvent
{
    public WorkflowEventType Type => WorkflowEventType.CUSTOM;
}
