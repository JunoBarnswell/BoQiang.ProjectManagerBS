using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_tool_definitions")]
public sealed class AiToolDefinitionEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ToolCode { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string ToolType { get; set; } = "Api";

    public string ToolDomain { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = "low";

    public bool RequiresConfirmation { get; set; }

    public string PermissionCode { get; set; } = string.Empty;

    public string InputSchemaJson { get; set; } = "{}";

    public string OutputSchemaJson { get; set; } = "{}";

    public string Status { get; set; } = "Enabled";
}
