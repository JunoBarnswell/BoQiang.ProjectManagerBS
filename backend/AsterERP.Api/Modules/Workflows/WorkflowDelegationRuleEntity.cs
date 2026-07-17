using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("workflow_delegation_rules")]
public sealed class WorkflowDelegationRuleEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string DelegateUserId { get; set; } = string.Empty;

    public string ScopeType { get; set; } = "All";

    [SugarColumn(IsNullable = true)]
    public string? ProcessDefinitionKey { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public bool IsEnabled { get; set; } = true;
}
