namespace AsterERP.Contracts.Ai;

public sealed class AiKernelFunctionDefinitionDto
{
    public string PluginName { get; set; } = string.Empty;

    public string FunctionName { get; set; } = string.Empty;

    public string ToolCode { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string ToolDomain { get; set; } = string.Empty;

    public string ToolVersion { get; set; } = "1.0";

    public string Description { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = "L0";

    public bool IsEnabled { get; set; } = true;

    public bool RequiresConfirmation { get; set; }

    public string PermissionCode { get; set; } = string.Empty;

    public string? WorkflowPermissionCode { get; set; }

    public IReadOnlyList<string> RequiredPermissionCodes { get; set; } = [];

    public IReadOnlyList<string> SensitiveArgumentNames { get; set; } = [];

    public IReadOnlyList<string> AllowedWorkModes { get; set; } = [];

    public IReadOnlyList<string> RequiredArgumentNames { get; set; } = [];

    public string InputSchemaJson { get; set; } = "{}";

    public string OutputSchemaJson { get; set; } = "{}";
}

public sealed class AiToolInvokeRequest
{
    public string? ConversationId { get; set; }

    public string? RunId { get; set; }

    public string? ModelConfigId { get; set; }

    public string? PlanId { get; set; }

    public string? PlanItemId { get; set; }

    public string? WorkMode { get; set; }

    public string? ArgumentsJson { get; set; }

    public Dictionary<string, object?> Arguments { get; set; } = [];

    public bool ConfirmedRiskAccepted { get; set; }
}

public sealed class AiToolDryRunResponse
{
    public string ToolCode { get; set; } = string.Empty;

    public bool IsValid { get; set; }

    public string RiskLevel { get; set; } = "L0";

    public string PermissionCode { get; set; } = string.Empty;

    public string? WorkflowPermissionCode { get; set; }

    public bool RequiresConfirmation { get; set; }

    public IReadOnlyList<string> Issues { get; set; } = [];

    public string NormalizedArgumentsJson { get; set; } = "{}";
}

public sealed class AiToolInvocationDto
{
    public string Id { get; set; } = string.Empty;

    public string? ConversationId { get; set; }

    public string? RunId { get; set; }

    public string? ModelConfigId { get; set; }

    public string? PlanId { get; set; }

    public string? ItemId { get; set; }

    public string ToolCode { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string? TraceId { get; set; }

    public string? ArgumentsJson { get; set; }

    public string? ResultSummary { get; set; }

    public string Status { get; set; } = "Pending";

    public int DurationMs { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class AiToolInvokeResponse
{
    public AiToolInvocationDto Invocation { get; set; } = new();

    public string ResultSummary { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? EvidenceJson { get; set; }

    public string OutputType { get; set; } = "Text";
}
