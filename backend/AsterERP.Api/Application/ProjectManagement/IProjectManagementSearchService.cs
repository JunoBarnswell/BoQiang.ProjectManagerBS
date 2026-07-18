using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementSearchService
{
    Task<ProjectManagementSearchResponse> SearchAsync(ProjectManagementSearchQuery query, CancellationToken cancellationToken = default);
    Task<ProjectManagementSearchIndexStatusResponse> GetIndexStatusAsync(CancellationToken cancellationToken = default);
    Task<ProjectManagementSearchIndexOperationResponse> QueueIndexRebuildAsync(ProjectManagementSearchIndexOperationRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementSearchIndexOperationResponse> QueueIndexIncrementalAsync(ProjectManagementSearchIndexOperationRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementSearchIndexOperationResponse> QueueIndexRecoveryAsync(ProjectManagementSearchIndexOperationRequest request, CancellationToken cancellationToken = default);
    Task<bool> TryExecuteIndexOperationAsync(ProjectManagementOperationJobArgs args, CancellationToken cancellationToken = default);
}
