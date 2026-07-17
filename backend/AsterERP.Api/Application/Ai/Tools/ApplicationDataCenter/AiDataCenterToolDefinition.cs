using System.Text.Json;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public static class AiDataCenterToolDefinition
{
    public static string AiReadPermission => PermissionCodes.AiToolDataCenterRead;

    public static string AiWritePermission => PermissionCodes.AiToolDataCenterWrite;

    public static string AiOperatePermission => PermissionCodes.AiToolDataCenterOperate;

    public static AiKernelFunctionDefinition Create(
        string toolCode,
        string toolName,
        string description,
        string riskLevel,
        string aiPermissionCode,
        string dataCenterPermissionCode,
        IReadOnlyList<string> workModes,
        IReadOnlyList<string>? requiredArguments = null,
        bool requiresConfirmation = false,
        IReadOnlyList<string>? sensitiveArguments = null)
    {
        return new AiKernelFunctionDefinition
        {
            ToolCode = toolCode,
            ToolName = toolName,
            ToolDomain = "data-center",
            Description = description,
            RiskLevel = riskLevel,
            PermissionCode = aiPermissionCode,
            RequiredPermissionCodes = [aiPermissionCode, dataCenterPermissionCode],
            AllowedWorkModes = workModes,
            RequiredArgumentNames = requiredArguments ?? [],
            SensitiveArgumentNames = sensitiveArguments ?? [],
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
