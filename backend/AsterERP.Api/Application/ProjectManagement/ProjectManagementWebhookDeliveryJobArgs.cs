namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementWebhookDeliveryJobArgs(string EventId, string TenantId, string AppCode, string ActorUserId);
