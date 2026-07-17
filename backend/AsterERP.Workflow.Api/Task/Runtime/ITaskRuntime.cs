using AsterERP.Workflow.Api.Task.Payload;

namespace AsterERP.Workflow.Api.Task.Runtime;

public interface ITaskRuntime
{
    Task<TaskPayload> CreateTaskAsync(CreateTaskPayload payload, CancellationToken cancellationToken = default);
    Task<TaskPayload> ClaimTaskAsync(ClaimTaskPayload payload, CancellationToken cancellationToken = default);
    Task<TaskPayload> ReleaseTaskAsync(ReleaseTaskPayload payload, CancellationToken cancellationToken = default);
    Task<TaskPayload> CompleteTaskAsync(CompleteTaskPayload payload, CancellationToken cancellationToken = default);
    Task<TaskPayload> UpdateTaskAsync(UpdateTaskPayload payload, CancellationToken cancellationToken = default);
    Task<TaskPayload> DeleteTaskAsync(DeleteTaskPayload payload, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TaskPayload>> GetTasksAsync(CancellationToken cancellationToken = default);
}
