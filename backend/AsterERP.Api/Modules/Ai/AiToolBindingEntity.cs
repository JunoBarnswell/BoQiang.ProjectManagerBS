using AsterERP.Domain.Common;

namespace AsterERP.Api.Modules.Ai;

[SqlSugar.SugarTable("ai_tool_bindings")]
public sealed class AiToolBindingEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string AgentProfileId { get; set; } = string.Empty;

    public string ToolCode { get; set; } = string.Empty;

    public bool AutoInvokeAllowed { get; set; }

    public string Status { get; set; } = "Enabled";
}
