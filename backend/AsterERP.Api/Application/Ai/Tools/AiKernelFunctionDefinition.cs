using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.Tools;

public sealed class AiKernelFunctionDefinition
{
    public string ToolCode { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public string ToolDomain { get; init; } = string.Empty;

    public string ToolVersion { get; init; } = "1.0";

    public string Description { get; init; } = string.Empty;

    public string RiskLevel { get; init; } = "L0";

    public bool IsEnabled { get; init; } = true;

    public bool RequiresConfirmation { get; init; }

    public string PermissionCode { get; init; } = string.Empty;

    public string? WorkflowPermissionCode { get; init; }

    public IReadOnlyList<string> RequiredPermissionCodes { get; init; } = [];

    public IReadOnlyList<string> SensitiveArgumentNames { get; init; } = [];

    public IReadOnlyList<string> AllowedWorkModes { get; init; } = [];

    public IReadOnlyList<string> RequiredArgumentNames { get; init; } = [];

    public string InputSchemaJson { get; init; } = "{}";

    public string OutputSchemaJson { get; init; } = "{}";

    public AiKernelFunctionName KernelName => AiKernelFunctionNaming.Resolve(this);

    public AiKernelFunctionDefinitionDto ToDto() => new()
    {
        PluginName = KernelName.PluginName,
        FunctionName = KernelName.FunctionName,
        ToolCode = ToolCode,
        ToolName = ToolName,
        ToolDomain = ToolDomain,
        ToolVersion = ToolVersion,
        Description = Description,
        RiskLevel = RiskLevel,
        IsEnabled = IsEnabled,
        RequiresConfirmation = RequiresConfirmation,
        PermissionCode = PermissionCode,
        WorkflowPermissionCode = WorkflowPermissionCode,
        RequiredPermissionCodes = RequiredPermissionCodes,
        SensitiveArgumentNames = SensitiveArgumentNames,
        AllowedWorkModes = AllowedWorkModes,
        RequiredArgumentNames = RequiredArgumentNames,
        InputSchemaJson = InputSchemaJson,
        OutputSchemaJson = OutputSchemaJson
    };
}
