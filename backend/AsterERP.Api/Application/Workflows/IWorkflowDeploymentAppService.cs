using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowDeploymentAppService
{
    Task<GridPageResult<WorkflowDeploymentListItemResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowProcessDefinitionResponse>> GetProcessDefinitionsAsync(string? key, CancellationToken cancellationToken = default);

    Task<WorkflowDeploymentResourceResponse> GetResourceAsync(string deploymentId, string resourceName, CancellationToken cancellationToken = default);
}
