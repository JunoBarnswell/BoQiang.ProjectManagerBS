using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowDelegationAppService
{
    Task<GridPageResult<WorkflowDelegationRuleResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<WorkflowDelegationRuleResponse> SaveAsync(WorkflowDelegationRuleUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
