using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.EventLogger.EventHandler;

public class ActivityCompletedEventHandler : IEventHandler
{
    public EventLogEntry? HandleEvent(IWorkflowEvent @event)
    {
        if (@event.Type != WorkflowEventType.ACTIVITY_COMPLETED) return null;

        var data = ExtractActivityData(@event);

        return new EventLogEntry
        {
            Type = "ACTIVITY_COMPLETED",
            ProcessDefinitionId = @event.ProcessDefinitionId,
            ProcessInstanceId = @event.ProcessInstanceId,
            ExecutionId = @event.ExecutionId,
            Data = data
        };
    }

    private static Dictionary<string, object?>? ExtractActivityData(IWorkflowEvent @event)
    {
        if (@event is WorkflowEntityEvent entityEvent && entityEvent.Entity != null)
        {
            var entity = entityEvent.Entity;
            var activityIdProp = entity.GetType().GetProperty("ActivityId");
            var activityTypeProp = entity.GetType().GetProperty("ActivityType");
            var activityId = activityIdProp?.GetValue(entity)?.ToString();
            var activityType = activityTypeProp?.GetValue(entity)?.ToString();

            return new Dictionary<string, object?>
            {
                ["ActivityId"] = activityId,
                ["ActivityType"] = activityType,
                ["EndTime"] = AbpTimeIdProvider.UtcNow
            };
        }
        return null;
    }
}

