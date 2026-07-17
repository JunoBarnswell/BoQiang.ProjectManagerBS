namespace AsterERP.Workflow.Core.History;

public interface IHistoricEntityService
{
    global::System.Threading.Tasks.Task RecordProcessInstanceStartAsync(string processInstanceId, string processDefinitionId, string? businessKey, string? startUserId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordProcessInstanceEndAsync(string processInstanceId, string? deleteReason, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordTaskCreatedAsync(string taskId, string taskName, string? assignee, string processInstanceId, string? taskDefinitionKey, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordTaskCompletedAsync(string taskId, string? assignee, string? deleteReason, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordActivityStartAsync(string id, string activityId, string activityName, string activityType, string executionId, string processInstanceId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordActivityEndAsync(string activityId, string executionId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordVariableAsync(string id, string variableName, object? value, string processInstanceId, string? taskId, bool includeDetail = true, CancellationToken cancellationToken = default);
}
