using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.EventLogger.EventHandler;

public class ProcessInstanceEndEventHandler : IEventHandler
{
    public EventLogEntry? HandleEvent(IWorkflowEvent @event)
    {
        if (@event.Type != WorkflowEventType.PROCESS_COMPLETED) return null;

        return new EventLogEntry
        {
            Type = "PROCESS_COMPLETED",
            ProcessDefinitionId = @event.ProcessDefinitionId,
            ProcessInstanceId = @event.ProcessInstanceId,
            ExecutionId = @event.ExecutionId,
            Data = new Dictionary<string, object?>
            {
                ["EndTime"] = AbpTimeIdProvider.UtcNow
            }
        };
    }
}

