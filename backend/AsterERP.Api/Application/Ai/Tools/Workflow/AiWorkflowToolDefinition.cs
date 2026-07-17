using AsterERP.Shared;
using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public static class AiWorkflowToolDefinition
{
    public static AiKernelFunctionDefinition Create(
        string toolCode,
        string toolName,
        string description,
        string riskLevel,
        string permissionCode,
        string? workflowPermissionCode,
        IReadOnlyList<string> workModes,
        IReadOnlyList<string>? requiredArguments = null,
        bool requiresConfirmation = false)
    {
        return new AiKernelFunctionDefinition
        {
            ToolCode = toolCode,
            ToolName = toolName,
            ToolDomain = "workflow",
            Description = description,
            RiskLevel = riskLevel,
            PermissionCode = permissionCode,
            WorkflowPermissionCode = workflowPermissionCode,
            AllowedWorkModes = workModes,
            RequiredArgumentNames = requiredArguments ?? [],
            RequiresConfirmation = requiresConfirmation,
            InputSchemaJson = $$"""
            {
              "type": "object",
              "required": {{JsonSerializer.Serialize(requiredArguments ?? [])}},
              "additionalProperties": true
            }
            """,
            OutputSchemaJson = """{ "type": "object" }"""
        };
    }
}
