using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.EventLogger.EventHandler;

public class ProcessInstanceStartEventHandler : IEventHandler
{
    public EventLogEntry? HandleEvent(IWorkflowEvent @event)
    {
        if (@event.Type != WorkflowEventType.PROCESS_STARTED) return null;

        var data = ExtractEntityData(@event);

        return new EventLogEntry
        {
            Type = "PROCESS_STARTED",
            ProcessDefinitionId = @event.ProcessDefinitionId,
            ProcessInstanceId = @event.ProcessInstanceId,
            ExecutionId = @event.ExecutionId,
            Data = data
        };
    }

    private static Dictionary<string, object?>? ExtractEntityData(IWorkflowEvent @event)
    {
        if (@event is WorkflowEntityEvent entityEvent && entityEvent.Entity != null)
        {
            return new Dictionary<string, object?>
            {
                ["BusinessKey"] = entityEvent.Entity
            };
        }
        return null;
    }
}
