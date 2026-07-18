using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementOperationProgressPublisher
{
    Task PublishAsync(string tenantId, string appCode, string userId, ProjectManagementOperationProgressEvent progressEvent, CancellationToken cancellationToken = default);
}
