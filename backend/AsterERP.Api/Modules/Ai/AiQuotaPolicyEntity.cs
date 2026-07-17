using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_quota_policies")]
public sealed class AiQuotaPolicyEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string PolicyName { get; set; } = string.Empty;

    public string ScopeType { get; set; } = "Tenant";

    [SugarColumn(IsNullable = true)]
    public string? ScopeId { get; set; }

    public int MaxRequestsPerDay { get; set; } = 1000;

    public int MaxTokensPerDay { get; set; } = 1000000;

    public int MaxConcurrentRuns { get; set; } = 10;

    public bool IsEnabled { get; set; } = true;
}
