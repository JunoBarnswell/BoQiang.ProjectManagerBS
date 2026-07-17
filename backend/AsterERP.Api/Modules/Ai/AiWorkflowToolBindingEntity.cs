using AsterERP.Domain.Common;

namespace AsterERP.Api.Modules.Ai;

[SqlSugar.SugarTable("ai_workflow_tool_bindings")]
public sealed class AiWorkflowToolBindingEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string WorkflowModelId { get; set; } = string.Empty;

    public string WorkflowCode { get; set; } = string.Empty;

    public string WorkflowName { get; set; } = string.Empty;

    public string ToolCode { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = "high";

    public bool RequiresConfirmation { get; set; } = true;

    public string Status { get; set; } = "Enabled";
}
