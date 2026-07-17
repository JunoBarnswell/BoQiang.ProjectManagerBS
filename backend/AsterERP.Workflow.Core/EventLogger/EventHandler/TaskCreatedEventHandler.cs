using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.EventLogger.EventHandler;

public class TaskCreatedEventHandler : IEventHandler
{
    public EventLogEntry? HandleEvent(IWorkflowEvent @event)
    {
        if (@event.Type != WorkflowEventType.TASK_CREATED) return null;

        var (taskId, data) = ExtractTaskData(@event);

        return new EventLogEntry
        {
            Type = "TASK_CREATED",
            ProcessDefinitionId = @event.ProcessDefinitionId,
            ProcessInstanceId = @event.ProcessInstanceId,
            ExecutionId = @event.ExecutionId,
            TaskId = taskId,
            Data = data
        };
    }

    private static (string? taskId, Dictionary<string, object?>? data) ExtractTaskData(IWorkflowEvent @event)
    {
        if (@event is WorkflowEntityEvent entityEvent && entityEvent.Entity != null)
        {
            var entity = entityEvent.Entity;
            var taskIdProp = entity.GetType().GetProperty("TaskId");
            var assigneeProp = entity.GetType().GetProperty("Assignee");
            var taskId = taskIdProp?.GetValue(entity)?.ToString();
            var assignee = assigneeProp?.GetValue(entity)?.ToString();

            return (taskId, new Dictionary<string, object?>
            {
                ["TaskId"] = taskId,
                ["Assignee"] = assignee
            });
        }
        return (null, null);
    }
}
