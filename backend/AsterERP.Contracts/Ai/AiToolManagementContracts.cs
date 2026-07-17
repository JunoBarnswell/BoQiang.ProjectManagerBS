using AsterERP.Shared;

namespace AsterERP.Contracts.Ai;

public sealed class AiToolDefinitionDto
{
    public string Id { get; set; } = string.Empty;

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

    public DateTime CreatedTime { get; set; }
}

public sealed class AiToolDefinitionUpsertRequest
{
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

public sealed class AiToolBindingDto
{
    public string Id { get; set; } = string.Empty;

    public string AgentProfileId { get; set; } = string.Empty;

    public string ToolCode { get; set; } = string.Empty;

    public bool AutoInvokeAllowed { get; set; }

    public string Status { get; set; } = "Enabled";
}

public sealed class AiToolBindingUpsertRequest
{
    public string AgentProfileId { get; set; } = string.Empty;

    public string ToolCode { get; set; } = string.Empty;

    public bool AutoInvokeAllowed { get; set; }

    public string Status { get; set; } = "Enabled";
}

public sealed class AiWorkflowToolBindingDto
{
    public string Id { get; set; } = string.Empty;

    public string WorkflowModelId { get; set; } = string.Empty;

    public string WorkflowCode { get; set; } = string.Empty;

    public string WorkflowName { get; set; } = string.Empty;

    public string ToolCode { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = "high";

    public bool RequiresConfirmation { get; set; } = true;

    public string Status { get; set; } = "Enabled";
}

public sealed class AiWorkflowToolBindingRequest
{
    public string WorkflowModelId { get; set; } = string.Empty;

    public string WorkflowCode { get; set; } = string.Empty;

    public string WorkflowName { get; set; } = string.Empty;

    public string ToolCode { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = "high";

    public bool RequiresConfirmation { get; set; } = true;

    public string Status { get; set; } = "Enabled";
}

public sealed class AiWorkflowOptionDto
{
    public string WorkflowModelId { get; set; } = string.Empty;

    public string WorkflowCode { get; set; } = string.Empty;

    public string WorkflowName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

public sealed class AiToolDefinitionQuery
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public string? Status { get; set; }

    public string? ToolType { get; set; }

    public string? ToolDomain { get; set; }

    public string? RiskLevel { get; set; }
}
