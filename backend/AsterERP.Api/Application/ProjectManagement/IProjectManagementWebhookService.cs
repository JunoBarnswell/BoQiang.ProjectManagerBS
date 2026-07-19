using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementWebhookService
{
    Task<IReadOnlyList<ProjectManagementWebhookSubscriptionResponse>> GetSubscriptionsAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementWebhookSubscriptionResponse> SaveSubscriptionAsync(ProjectManagementWebhookSubscriptionUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteSubscriptionAsync(string id, CancellationToken cancellationToken = default);
    Task<GridPageResult<ProjectManagementWebhookDeliveryResponse>> GetDeliveriesAsync(string projectId, GridQuery query, CancellationToken cancellationToken = default);
    Task<ProjectManagementWebhookDeliveryResponse> ReplayAsync(string eventId, ProjectManagementWebhookReplayRequest request, CancellationToken cancellationToken = default);
    Task PublishActivityAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default);
    Task DeliverAsync(ProjectManagementWebhookDeliveryJobArgs args, CancellationToken cancellationToken = default);
}
