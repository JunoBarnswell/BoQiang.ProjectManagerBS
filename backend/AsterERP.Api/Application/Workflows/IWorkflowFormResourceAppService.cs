using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowFormResourceAppService
{
    Task<GridPageResult<WorkflowFormResourceResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<WorkflowFormResourceResponse?> ValidateBindingResourceAsync(
        WorkflowBindingUpsertRequest request,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetFieldLabelsForBindingAsync(
        string tenantId,
        string appCode,
        string menuCode,
        string businessType,
        CancellationToken cancellationToken = default);
}
