using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.EventLogger.EventHandler;

public class TaskCompletedEventHandler : IEventHandler
{
    public EventLogEntry? HandleEvent(IWorkflowEvent @event)
    {
        if (@event.Type != WorkflowEventType.TASK_COMPLETED) return null;

        var (taskId, data) = ExtractTaskData(@event);

        return new EventLogEntry
        {
            Type = "TASK_COMPLETED",
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
            var taskId = taskIdProp?.GetValue(entity)?.ToString();

            return (taskId, new Dictionary<string, object?>
            {
                ["TaskId"] = taskId,
                ["CompletedTime"] = AbpTimeIdProvider.UtcNow
            });
        }
        return (null, null);
    }
}

