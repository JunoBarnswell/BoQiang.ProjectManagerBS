using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowInstanceAppService
{
    Task<GridPageResult<WorkflowInstanceListItemResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowInstanceListItemResponse>> GetMineAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<WorkflowInstanceResponse> StartAsync(WorkflowStartInstanceRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowInstanceResponse> GetDetailAsync(string processInstanceId, CancellationToken cancellationToken = default);

    Task WithdrawAsync(string processInstanceId, string? reason, CancellationToken cancellationToken = default);

    Task TerminateAsync(string processInstanceId, string? reason, CancellationToken cancellationToken = default);

    Task<WorkflowInstanceResponse> SetVariablesAsync(string processInstanceId, WorkflowInstanceVariableRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowHighlightedDiagramResponse> GetHighlightedDiagramAsync(string processInstanceId, CancellationToken cancellationToken = default);

    Task SignalAsync(string executionId, WorkflowInstanceVariableRequest? request, CancellationToken cancellationToken = default);

    Task MessageAsync(string executionId, string messageName, WorkflowInstanceVariableRequest? request, CancellationToken cancellationToken = default);
}
