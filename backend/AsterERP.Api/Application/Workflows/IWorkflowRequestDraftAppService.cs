using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowRequestDraftAppService
{
    Task<GridPageResult<WorkflowRequestDraftResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<WorkflowRequestDraftResponse> SaveAsync(WorkflowRequestDraftUpsertRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowInstanceResponse> SubmitAsync(string id, WorkflowRequestDraftSubmitRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
