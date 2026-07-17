using AsterERP.Workflow.Api.Task.Payload;

namespace AsterERP.Workflow.Api.Task.Runtime;

public interface ITaskAdminRuntime
{
    Task<TaskPayload> ClaimTaskAsync(string taskId, string assignee, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TaskPayload>> GetAllTasksAsync(CancellationToken cancellationToken = default);
}
