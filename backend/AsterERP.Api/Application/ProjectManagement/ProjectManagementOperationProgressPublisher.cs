using AsterERP.Api.Infrastructure.SignalR;
using AsterERP.Contracts.ProjectManagement;
using Microsoft.AspNetCore.SignalR;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementOperationProgressPublisher(IHubContext<SystemNotificationHub> hubContext) : IProjectManagementOperationProgressPublisher
{
    public Task PublishAsync(string tenantId, string appCode, string userId, ProjectManagementOperationProgressEvent progressEvent, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(SystemNotificationHub.BuildProjectManagementOperationUserGroupName(tenantId, appCode, userId))
            .SendAsync("ProjectManagementOperationProgressUpdated", progressEvent, cancellationToken);
}
