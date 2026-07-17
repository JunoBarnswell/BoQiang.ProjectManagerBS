namespace AsterERP.Workflow.Core.Event;

public enum WorkflowEventType
{
    ENTITY_CREATED,
    ENTITY_SUSPENDED,
    ENTITY_ACTIVATED,
    ENTITY_UPDATED,
    ENTITY_DELETED,
    ENTITY_INITIALIZED,
    TIMER_SCHEDULED,
    TIMER_FIRED,
    ENGINE_CREATED,
    ENGINE_CLOSED,
    PROCESS_STARTED,
    PROCESS_COMPLETED,
    PROCESS_COMPLETED_WITH_ERROR_END_EVENT,
    PROCESS_CANCELLED,
    HISTORIC_PROCESS_INSTANCE_CREATED,
    HISTORIC_PROCESS_INSTANCE_ENDED,
    TASK_CREATED,
    TASK_ASSIGNED,
    TASK_COMPLETED,
    ACTIVITY_STARTED,
    ACTIVITY_COMPLETED,
    ACTIVITY_CANCELLED,
    ACTIVITY_SIGNALED,
    ACTIVITY_COMPENSATE,
    ACTIVITY_MESSAGE_SENT,
    ACTIVITY_MESSAGE_WAITING,
    ACTIVITY_MESSAGE_RECEIVED,
    ACTIVITY_ERROR_RECEIVED,
    HISTORIC_ACTIVITY_INSTANCE_CREATED,
    HISTORIC_ACTIVITY_INSTANCE_ENDED,
    SEQUENCEFLOW_TAKEN,
    UNCAUGHT_BPMN_ERROR,
    VARIABLE_CREATED,
    VARIABLE_UPDATED,
    VARIABLE_DELETED,
    MEMBERSHIP_CREATED,
    MEMBERSHIP_DELETED,
    MEMBERSHIPS_DELETED,
    JOB_EXECUTION_SUCCESS,
    JOB_EXECUTION_FAILURE,
    JOB_CANCELED,
    JOB_RETRIES_DECREMENTED,
    CUSTOM
}

public interface IWorkflowEvent
{
    WorkflowEventType Type { get; }
    string? ExecutionId { get; }
    string? ProcessInstanceId { get; }
    string? ProcessDefinitionId { get; }
}

public class WorkflowEventImplementation : IWorkflowEvent
{
    public WorkflowEventType Type { get; }
    public string? ExecutionId { get; }
    public string? ProcessInstanceId { get; }
    public string? ProcessDefinitionId { get; }

    public WorkflowEventImplementation(
        WorkflowEventType type,
        string? executionId = null,
        string? processInstanceId = null,
        string? processDefinitionId = null)
    {
        Type = type;
        ExecutionId = executionId;
        ProcessInstanceId = processInstanceId;
        ProcessDefinitionId = processDefinitionId;
    }
}
