using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_security_policies")]
public sealed class AiSecurityPolicyEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string PolicyKey { get; set; } = string.Empty;

    public string PolicyValue { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;
}
