using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_custom_mcp_servers")]
public sealed class FlowiseCustomMcpServerEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ServerUrl { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? IconSrc { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Color { get; set; }

    public string AuthType { get; set; } = "none";

    [SugarColumn(IsNullable = true)]
    public string? AuthConfigCipherText { get; set; }

    public string AuthConfigMaskJson { get; set; } = "{}";

    public string ToolsJson { get; set; } = "[]";

    public int ToolCount { get; set; }

    public string Status { get; set; } = "Enabled";

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }
}
