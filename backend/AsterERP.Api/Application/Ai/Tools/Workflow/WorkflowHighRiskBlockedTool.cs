using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowHighRiskBlockedTool : IAiKernelFunction
{
    public WorkflowHighRiskBlockedTool(string toolCode, string toolName, string description)
    {
        Definition = AiWorkflowToolDefinition.Create(
            toolCode,
            toolName,
            description,
            "L4",
            PermissionCodes.AiToolWorkflowPublishRequest,
            null,
            [],
            [],
            requiresConfirmation: true);
    }

    public AiKernelFunctionDefinition Definition { get; }

    public Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        throw new ValidationException(
            "该 Workflow 工具属于 L4 高风险操作，首期仅注册和审计拦截，必须由人工在正式 Workflow 页面执行。",
            ErrorCodes.AiWorkflowHighRiskBlocked);
    }
}
