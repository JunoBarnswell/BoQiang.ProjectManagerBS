namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IWorkflowActivityInstanceService
{
    Task SyncRuntimeTasksAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task FinishRuntimeTaskAsync(string taskId, CancellationToken cancellationToken = default);
    Task DeleteActinstByIdsAsync(List<string> actIds, CancellationToken cancellationToken = default);
}
