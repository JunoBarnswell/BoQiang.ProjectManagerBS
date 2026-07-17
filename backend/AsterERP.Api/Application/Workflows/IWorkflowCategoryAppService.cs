using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowCategoryAppService
{
    Task<GridPageResult<WorkflowCategoryResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<WorkflowCategoryResponse> SaveAsync(WorkflowCategoryUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
