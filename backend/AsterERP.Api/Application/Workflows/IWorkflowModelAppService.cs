using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowModelAppService
{
    Task<GridPageResult<WorkflowModelListItemResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<WorkflowModelDetailResponse> GetDetailAsync(string modelId, CancellationToken cancellationToken = default);

    Task<WorkflowModelDetailResponse> CreateOrUpdateAsync(WorkflowModelUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteDraftAsync(string modelId, CancellationToken cancellationToken = default);

    Task<WorkflowModelDetailResponse> SaveXmlAsync(string modelId, WorkflowModelXmlSaveRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowModelDetailResponse> ImportAiDraftAsync(string draftArtifactId, CancellationToken cancellationToken = default);

    Task<WorkflowModelValidationResponse> ValidateAsync(string modelId, CancellationToken cancellationToken = default);

    Task<WorkflowModelPublishResponse> PublishAsync(string modelId, CancellationToken cancellationToken = default);

    Task SuspendAsync(string processDefinitionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowModelVersionResponse>> GetVersionsAsync(string modelKey, CancellationToken cancellationToken = default);
}
