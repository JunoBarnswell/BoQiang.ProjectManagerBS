using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementProjectSubscriptionService
{
    Task<ProjectManagementProjectSubscriptionResponse?> QueryAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementProjectSubscriptionResponse> SaveAsync(string projectId, ProjectManagementProjectSubscriptionUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, long? versionNo, CancellationToken cancellationToken = default);
}
