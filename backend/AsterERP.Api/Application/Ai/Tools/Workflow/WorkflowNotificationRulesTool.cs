using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowNotificationRulesTool(IWorkflowNotificationAppService notificationService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.NotificationGetRules,
        "查询通知规则",
        "查看 Workflow 节点通知规则，不发送通知",
        "L1",
        PermissionCodes.AiToolWorkflowRead,
        PermissionCodes.WorkflowNotificationRuleQuery,
        ["Ask", "Plan", "Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var page = await notificationService.GetRulesAsync(new WorkflowNotificationQuery(
            PageIndex: 1,
            PageSize: Math.Clamp(AiWorkflowArgumentReader.ReadInt(context.Arguments, "pageSize", 20), 1, 100),
            Keyword: AiWorkflowArgumentReader.ReadString(context.Arguments, "keyword"),
            TenantId: context.TenantId,
            AppCode: context.AppCode), cancellationToken);
        return Result($"查询到 {page.Total} 条通知规则", page, Evidence(("count", page.Total)));
    }
}
