namespace AsterERP.Workflow.Core.Event;

public class WorkflowEntityEvent : IWorkflowEvent
{
    public WorkflowEventType Type { get; }
    public string? ExecutionId { get; }
    public string? ProcessInstanceId { get; }
    public string? ProcessDefinitionId { get; }
    public object? Entity { get; }

    public WorkflowEntityEvent(
        WorkflowEventType type,
        object? entity = null,
        string? executionId = null,
        string? processInstanceId = null,
        string? processDefinitionId = null)
    {
        Type = type;
        Entity = entity;
        ExecutionId = executionId;
        ProcessInstanceId = processInstanceId;
        ProcessDefinitionId = processDefinitionId;
    }
}

public class WorkflowCustomEvent : IWorkflowEvent
{
    public WorkflowEventType Type { get; }
    public string? ExecutionId { get; }
    public string? ProcessInstanceId { get; }
    public string? ProcessDefinitionId { get; }
    public string EventName { get; }
    public Dictionary<string, object?> Data { get; }

    public WorkflowCustomEvent(
        string eventName,
        Dictionary<string, object?> data,
        string? executionId = null,
        string? processInstanceId = null,
        string? processDefinitionId = null)
    {
        Type = WorkflowEventType.CUSTOM;
        EventName = eventName;
        Data = data;
        ExecutionId = executionId;
        ProcessInstanceId = processInstanceId;
        ProcessDefinitionId = processDefinitionId;
    }
}

public static class WorkflowEventBuilder
{
    public static IWorkflowEvent CreateEntityEvent(WorkflowEventType type, object entity, string? executionId = null, string? processInstanceId = null, string? processDefinitionId = null)
    {
        return new WorkflowEntityEvent(type, entity, executionId, processInstanceId, processDefinitionId);
    }

    public static IWorkflowEvent CreateProcessStartedEvent(string processInstanceId, string processDefinitionId, string? businessKey)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.PROCESS_STARTED,
            businessKey,
            processInstanceId: processInstanceId,
            processDefinitionId: processDefinitionId);
    }

    public static IWorkflowEvent CreateProcessCompletedEvent(string processInstanceId, string processDefinitionId)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.PROCESS_COMPLETED,
            processInstanceId: processInstanceId,
            processDefinitionId: processDefinitionId);
    }

    public static IWorkflowEvent CreateTaskCreatedEvent(string taskId, string? assignee, string processInstanceId)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.TASK_CREATED,
            new { TaskId = taskId, Assignee = assignee },
            processInstanceId: processInstanceId);
    }

    public static IWorkflowEvent CreateTaskAssignedEvent(string taskId, string assignee, string processInstanceId)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.TASK_ASSIGNED,
            new { TaskId = taskId, Assignee = assignee },
            processInstanceId: processInstanceId);
    }

    public static IWorkflowEvent CreateTaskCompletedEvent(string taskId, string processInstanceId)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.TASK_COMPLETED,
            new { TaskId = taskId },
            processInstanceId: processInstanceId);
    }

    public static IWorkflowEvent CreateActivityStartedEvent(string activityId, string activityType, string executionId, string processInstanceId)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.ACTIVITY_STARTED,
            new { ActivityId = activityId, ActivityType = activityType },
            executionId: executionId,
            processInstanceId: processInstanceId);
    }

    public static IWorkflowEvent CreateActivityCompletedEvent(string activityId, string activityType, string executionId, string processInstanceId)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.ACTIVITY_COMPLETED,
            new { ActivityId = activityId, ActivityType = activityType },
            executionId: executionId,
            processInstanceId: processInstanceId);
    }

    public static IWorkflowEvent CreateSequenceFlowTakenEvent(
        string sequenceFlowId,
        string? sourceActivityId,
        string? targetActivityId,
        string executionId,
        string processInstanceId)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.SEQUENCEFLOW_TAKEN,
            new { SequenceFlowId = sequenceFlowId, SourceActivityId = sourceActivityId, TargetActivityId = targetActivityId },
            executionId: executionId,
            processInstanceId: processInstanceId);
    }

    public static IWorkflowEvent CreateVariableCreatedEvent(string variableName, object? value, string executionId, string processInstanceId)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.VARIABLE_CREATED,
            new { VariableName = variableName, Value = value },
            executionId: executionId,
            processInstanceId: processInstanceId);
    }

    public static IWorkflowEvent CreateVariableUpdatedEvent(string variableName, object? value, string executionId, string processInstanceId)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.VARIABLE_UPDATED,
            new { VariableName = variableName, Value = value },
            executionId: executionId,
            processInstanceId: processInstanceId);
    }

    public static IWorkflowEvent CreateVariableDeletedEvent(string variableName, string executionId, string processInstanceId)
    {
        return new WorkflowEntityEvent(
            WorkflowEventType.VARIABLE_DELETED,
            new { VariableName = variableName },
            executionId: executionId,
            processInstanceId: processInstanceId);
    }

    public static IWorkflowEvent CreateCustomEvent(string eventName, Dictionary<string, object?> data)
    {
        return new WorkflowCustomEvent(eventName, data);
    }
}
