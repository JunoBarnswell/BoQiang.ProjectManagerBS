using AsterERP.Workflow.Api.Process.Payload;

namespace AsterERP.Workflow.Api.Process.Runtime;

public interface IProcessAdminRuntime
{
    Task<ProcessInstancePayload> SuspendProcessInstanceByIdAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default);

    Task<ProcessInstancePayload> ActivateProcessInstanceByIdAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default);

    global::System.Threading.Tasks.Task DeleteProcessInstanceAsync(
        string processInstanceId,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
