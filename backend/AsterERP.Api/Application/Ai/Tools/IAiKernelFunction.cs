using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.Tools;

public interface IAiKernelFunction
{
    AiKernelFunctionDefinition Definition { get; }

    Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken);

    Task<AiToolDryRunResponse> DryRunAsync(
        AiKernelFunctionContext context,
        IReadOnlyList<string> validationIssues,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new AiToolDryRunResponse
        {
            ToolCode = Definition.ToolCode,
            IsValid = validationIssues.Count == 0,
            RiskLevel = Definition.RiskLevel,
            PermissionCode = Definition.PermissionCode,
            WorkflowPermissionCode = Definition.WorkflowPermissionCode,
            RequiresConfirmation = Definition.RequiresConfirmation,
            Issues = validationIssues,
            NormalizedArgumentsJson = context.ArgumentsJson
        });
    }
}
