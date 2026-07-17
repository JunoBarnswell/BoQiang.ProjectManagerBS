using AsterERP.Workflow.Api.Process.Payload;
using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Api.Process.Runtime;

public class ProcessAdminRuntimeImplementation : IProcessAdminRuntime
{
    private readonly IRuntimeService _runtimeService;
    private readonly IHistoryService _historyService;

    public ProcessAdminRuntimeImplementation(
        IRuntimeService runtimeService,
        IHistoryService historyService)
    {
        _runtimeService = runtimeService;
        _historyService = historyService;
    }

    public async Task<ProcessInstancePayload> SuspendProcessInstanceByIdAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        await _runtimeService.SuspendProcessInstanceByIdAsync(processInstanceId, cancellationToken);

        return await GetRuntimeProcessInstanceAsync(processInstanceId, cancellationToken);
    }

    public async Task<ProcessInstancePayload> ActivateProcessInstanceByIdAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        await _runtimeService.ActivateProcessInstanceByIdAsync(processInstanceId, cancellationToken);

        return await GetRuntimeProcessInstanceAsync(processInstanceId, cancellationToken);
    }

    public global::System.Threading.Tasks.Task DeleteProcessInstanceAsync(
        string processInstanceId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        return _runtimeService.DeleteProcessInstanceAsync(processInstanceId, reason, cancellationToken);
    }

    private async Task<ProcessInstancePayload> GetRuntimeProcessInstanceAsync(
        string processInstanceId,
        CancellationToken cancellationToken)
    {
        var executions = await _runtimeService.GetExecutionsAsync(cancellationToken);
        var relatedExecutions = executions
            .Where(e => e.ProcessInstanceId == processInstanceId || e.Id == processInstanceId)
            .ToList();

        if (relatedExecutions.Count == 0)
            throw new WorkflowNotFoundException($"Process instance '{processInstanceId}' not found");

        var root = relatedExecutions.FirstOrDefault(e => e.Id == processInstanceId) ?? relatedExecutions.First();

        return new ProcessInstancePayload
        {
            Id = processInstanceId,
            ProcessDefinitionId = root.ProcessDefinitionId ?? "",
            BusinessKey = root.BusinessKey,
            Status = GetRuntimeStatus(relatedExecutions)
        };
    }

    private static ProcessInstanceStatus GetRuntimeStatus(IReadOnlyCollection<ExecutionRecord> executions)
    {
        if (executions.Any(e => e.IsActive))
            return ProcessInstanceStatus.Running;

        return executions.All(e => e.IsEnded)
            ? ProcessInstanceStatus.Completed
            : ProcessInstanceStatus.Suspended;
    }
}
