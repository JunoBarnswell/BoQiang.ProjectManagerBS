using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowModelSearchTool(IWorkflowModelAppService workflowModelService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.ModelSearch,
        "查询流程模型",
        "按名称、业务类型、状态或版本查询 Workflow 模型",
        "L1",
        PermissionCodes.AiToolWorkflowRead,
        PermissionCodes.WorkflowModelQuery,
        ["Ask", "Plan", "Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var query = new GridQuery
        {
            PageIndex = 1,
            PageSize = Math.Clamp(AiWorkflowArgumentReader.ReadInt(context.Arguments, "pageSize", 10), 1, 100),
            Keyword = AiWorkflowArgumentReader.ReadString(context.Arguments, "keyword") ??
                      AiWorkflowArgumentReader.ReadString(context.Arguments, "requirementText"),
            Status = AiWorkflowArgumentReader.ReadString(context.Arguments, "status"),
            AppCode = context.AppCode
        };
        var page = await workflowModelService.GetPageAsync(query, cancellationToken);
        return Result($"查询到 {page.Total} 个流程模型", page, Evidence(("toolCode", Definition.ToolCode), ("count", page.Total)));
    }
}
