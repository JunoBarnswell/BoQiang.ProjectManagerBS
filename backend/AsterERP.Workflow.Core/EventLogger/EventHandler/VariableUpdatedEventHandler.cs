using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.EventLogger.EventHandler;

public class VariableUpdatedEventHandler : IEventHandler
{
    public EventLogEntry? HandleEvent(IWorkflowEvent @event)
    {
        if (@event.Type != WorkflowEventType.VARIABLE_UPDATED) return null;

        var data = ExtractVariableData(@event);

        return new EventLogEntry
        {
            Type = "VARIABLE_UPDATED",
            ProcessDefinitionId = @event.ProcessDefinitionId,
            ProcessInstanceId = @event.ProcessInstanceId,
            ExecutionId = @event.ExecutionId,
            Data = data
        };
    }

    private static Dictionary<string, object?>? ExtractVariableData(IWorkflowEvent @event)
    {
        if (@event is WorkflowEntityEvent entityEvent && entityEvent.Entity != null)
        {
            var entity = entityEvent.Entity;
            var variableNameProp = entity.GetType().GetProperty("VariableName");
            var valueProp = entity.GetType().GetProperty("Value");
            var variableName = variableNameProp?.GetValue(entity)?.ToString();
            var value = valueProp?.GetValue(entity);

            return new Dictionary<string, object?>
            {
                ["VariableName"] = variableName,
                ["Value"] = value,
                ["UpdatedTime"] = AbpTimeIdProvider.UtcNow
            };
        }
        return null;
    }
}

