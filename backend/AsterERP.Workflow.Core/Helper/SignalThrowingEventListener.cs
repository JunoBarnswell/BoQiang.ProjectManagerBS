using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Helper;

public class SignalThrowingEventListener : BaseDelegateEventListener
{
    public string? SignalName { get; set; }
    public bool ProcessInstanceScope { get; set; } = true;
    public List<SignalThrownWorkflowEvent> ThrownSignals { get; } = new();

    public override bool IsFailOnException => true;

    public override void OnEvent(IWorkflowEvent @event)
    {
        if (!IsValidEvent(@event))
            return;

        if (string.IsNullOrWhiteSpace(SignalName))
            throw new WorkflowEngineArgumentException("signalName is null or empty");

        if (ProcessInstanceScope && string.IsNullOrEmpty(@event.ProcessInstanceId))
        {
            throw new WorkflowEngineArgumentException(
                "Cannot throw process-instance scoped signal, since the dispatched event is not part of an ongoing process instance");
        }

        ThrownSignals.Add(new SignalThrownWorkflowEvent(
            SignalName,
            ProcessInstanceScope,
            @event.ExecutionId,
            @event.ProcessInstanceId,
            @event.ProcessDefinitionId));
    }
}
