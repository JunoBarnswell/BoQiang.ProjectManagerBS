using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowBindingSearchTool(IWorkflowBindingAppService bindingService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.BindingSearch,
        "查询业务绑定",
        "查询业务单据与 Workflow 的绑定状态",
        "L1",
        PermissionCodes.AiToolWorkflowRead,
        PermissionCodes.WorkflowBindingQuery,
        ["Ask", "Plan", "Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var query = new GridQuery
        {
            PageIndex = 1,
            PageSize = Math.Clamp(AiWorkflowArgumentReader.ReadInt(context.Arguments, "pageSize", 20), 1, 100),
            Keyword = AiWorkflowArgumentReader.ReadString(context.Arguments, "keyword") ??
                      AiWorkflowArgumentReader.ReadString(context.Arguments, "businessType"),
            TenantId = context.TenantId,
            AppCode = context.AppCode
        };
        var page = await bindingService.GetPageAsync(query, cancellationToken);
        return Result($"查询到 {page.Total} 条 Workflow 业务绑定", page, Evidence(("count", page.Total)));
    }
}
