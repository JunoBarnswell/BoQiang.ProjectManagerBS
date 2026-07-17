using AsterERP.Workflow.Api.Process.Payload;

namespace AsterERP.Workflow.Api.Process.Runtime;

public interface IProcessRuntime
{
    Task<ProcessDefinitionPayload> DeployAsync(
        DeployPayload payload,
        CancellationToken cancellationToken = default);

    Task<ProcessInstancePayload> StartAsync(
        StartPayload payload,
        CancellationToken cancellationToken = default);

    Task<ProcessInstancePayload> SuspendAsync(
        SuspendPayload payload,
        CancellationToken cancellationToken = default);

    Task<ProcessInstancePayload> ResumeAsync(
        ResumePayload payload,
        CancellationToken cancellationToken = default);

    Task<ProcessInstancePayload> DeleteAsync(
        DeletePayload payload,
        CancellationToken cancellationToken = default);

    Task<ProcessDefinitionPayload> GetProcessDefinitionAsync(
        GetProcessDefinitionPayload payload,
        CancellationToken cancellationToken = default);

    Task<ProcessInstancePayload> GetProcessInstanceAsync(
        GetProcessInstancePayload payload,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProcessDefinitionPayload>> GetProcessDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProcessInstancePayload>> GetProcessInstancesAsync(
        CancellationToken cancellationToken = default);
}
