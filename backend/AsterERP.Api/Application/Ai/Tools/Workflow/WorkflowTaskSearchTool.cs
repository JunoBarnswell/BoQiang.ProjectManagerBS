using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowTaskSearchTool(IWorkflowTaskAppService taskService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.TaskSearch,
        "查询待办已办",
        "查询当前用户可见的 Workflow 任务",
        "L1",
        PermissionCodes.AiToolWorkflowRead,
        PermissionCodes.WorkflowTaskQuery,
        ["Ask", "Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var scope = AiWorkflowArgumentReader.ReadString(context.Arguments, "scope") ?? "todo";
        var query = new GridQuery
        {
            PageIndex = 1,
            PageSize = Math.Clamp(AiWorkflowArgumentReader.ReadInt(context.Arguments, "pageSize", 20), 1, 100),
            Keyword = AiWorkflowArgumentReader.ReadString(context.Arguments, "keyword")
        };
        object result = scope.Equals("done", StringComparison.OrdinalIgnoreCase)
            ? await taskService.GetDoneAsync(query, cancellationToken)
            : await taskService.GetTodoAsync(query, cancellationToken);
        return Result($"已查询 {scope} 任务", result, Evidence(("scope", scope)));
    }
}
