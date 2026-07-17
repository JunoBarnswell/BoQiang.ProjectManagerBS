using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowBindingAppService
{
    Task<GridPageResult<WorkflowBindingResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<WorkflowBindingResponse> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<WorkflowBindingResponse> SaveAsync(WorkflowBindingUpsertRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowBindingStatusResponse> GetStatusAsync(WorkflowBindingStatusRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
