using System;

namespace AsterERP.Workflow.Core.Deployer;

public class EventSubscriptionInfo
{
    public string Id { get; set; } = AbpTimeIdProvider.NewGuid("N");
    public string EventType { get; set; } = null!;
    public string EventName { get; set; } = null!;
    public string ActivityId { get; set; } = null!;
    public string ProcessDefinitionId { get; set; } = null!;
    public string? ProcessDefinitionKey { get; set; }
    public string? DeploymentId { get; set; }
    public string? TenantId { get; set; }
    public string? Configuration { get; set; }
    public DateTime Created { get; set; } = AbpTimeIdProvider.UtcNow;
}

