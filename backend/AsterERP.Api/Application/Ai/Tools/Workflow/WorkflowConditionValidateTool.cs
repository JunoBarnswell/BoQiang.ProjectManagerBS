using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowConditionValidateTool(WorkflowConditionEvaluator conditionEvaluator) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.ConditionValidate,
        "校验条件表达式",
        "校验 Workflow 条件表达式是否符合安全白名单",
        "L0",
        PermissionCodes.AiToolWorkflowValidate,
        PermissionCodes.WorkflowModelQuery,
        ["Plan", "Agent"],
        ["condition"]))
{
    public override Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var condition = AiWorkflowArgumentReader.ReadString(context.Arguments, "condition");
        var issue = conditionEvaluator.Validate(condition);
        var payload = new { condition, isValid = issue is null, issue };
        return Task.FromResult(Result(issue is null ? "条件表达式校验通过" : "条件表达式校验失败", payload, Evidence(("condition", condition), ("isValid", issue is null))));
    }
}
