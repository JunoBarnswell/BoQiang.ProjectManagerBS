using System.Text.Json;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.SystemAdministration;

public static class AiSystemAdminToolDefinition
{
    public static AiKernelFunctionDefinition Create(
        string toolCode,
        string toolName,
        string description,
        string riskLevel,
        string aiPermissionCode,
        string systemPermissionCode,
        IReadOnlyList<string> workModes,
        IReadOnlyList<string>? requiredArguments = null,
        IReadOnlyList<string>? sensitiveArguments = null,
        bool requiresConfirmation = false)
    {
        return new AiKernelFunctionDefinition
        {
            ToolCode = toolCode,
            ToolName = toolName,
            ToolDomain = AiSystemAdminToolCodes.Domain,
            Description = description,
            RiskLevel = riskLevel,
            PermissionCode = aiPermissionCode,
            WorkflowPermissionCode = systemPermissionCode,
            RequiredPermissionCodes = [aiPermissionCode, systemPermissionCode],
            SensitiveArgumentNames = sensitiveArguments ?? [],
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

    public static string AiReadPermission => PermissionCodes.AiToolSystemAdminRead;

    public static string AiWritePermission => PermissionCodes.AiToolSystemAdminWrite;

    public static string AiGrantPermission => PermissionCodes.AiToolSystemAdminGrant;

    public static string AiOperatePermission => PermissionCodes.AiToolSystemAdminOperate;
}
