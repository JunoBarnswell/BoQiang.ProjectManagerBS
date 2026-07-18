using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementOperationProgressPublisher(IProjectManagementRealtimeTransport realtimeTransport) : IProjectManagementOperationProgressPublisher
{
    public Task PublishAsync(string tenantId, string appCode, string userId, ProjectManagementOperationProgressEvent progressEvent, CancellationToken cancellationToken = default) =>
        realtimeTransport.PublishOperationProgressAsync(tenantId, appCode, userId, progressEvent, cancellationToken);
}
