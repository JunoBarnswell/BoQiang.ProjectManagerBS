namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementAutomationWebhookJobArgs(
    string DeliveryId,
    string TenantId,
    string AppCode,
    string ActorUserId,
    string TraceId);
