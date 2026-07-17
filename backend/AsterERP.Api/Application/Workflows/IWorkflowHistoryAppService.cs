using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowHistoryAppService
{
    Task<GridPageResult<WorkflowHistoricProcessResponse>> GetProcessesAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowHistoricTaskResponse>> GetTasksAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowActivityResponse>> GetActivitiesAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowHistoricVariableResponse>> GetVariablesAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowIdentityLinkResponse>> GetIdentityLinksAsync(string processInstanceId, CancellationToken cancellationToken = default);
}
