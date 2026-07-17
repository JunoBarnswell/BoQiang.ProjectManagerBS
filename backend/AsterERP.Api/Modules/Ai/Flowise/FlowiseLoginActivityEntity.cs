using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_login_activity")]
public sealed class FlowiseLoginActivityEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string UserName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? IpAddress { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? UserAgent { get; set; }

    public string Status { get; set; } = "Success";

    public string DetailJson { get; set; } = "{}";
}
