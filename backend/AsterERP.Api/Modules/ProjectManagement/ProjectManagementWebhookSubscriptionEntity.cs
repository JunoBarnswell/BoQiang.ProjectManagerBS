using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_webhook_subscriptions")]
public sealed class ProjectManagementWebhookSubscriptionEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string SecretCipherText { get; set; } = string.Empty;
    public string EventTypesJson { get; set; } = "[]";
    public bool IsEnabled { get; set; } = true;
    public int MaxAttempts { get; set; } = 5;
    public string OwnerUserId { get; set; } = string.Empty;
}
