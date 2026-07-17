using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowModelCompareVersionsTool(IWorkflowModelAppService workflowModelService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.ModelCompareVersions,
        "对比流程版本",
        "读取同一模型 Key 的版本列表并输出差异关注点",
        "L1",
        PermissionCodes.AiToolWorkflowRead,
        PermissionCodes.WorkflowModelQuery,
        ["Ask", "Plan", "Agent"],
        ["modelKey"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var modelKey = AiWorkflowArgumentReader.ReadString(context.Arguments, "modelKey")!;
        var versions = await workflowModelService.GetVersionsAsync(modelKey, cancellationToken);
        var payload = new
        {
            modelKey,
            versions,
            summary = versions.Count < 2 ? "当前可对比版本不足两个" : $"最新版本 {versions[0].Version}，上一版本 {versions[1].Version}，请在设计器中核对节点、条件、通知和绑定差异。"
        };
        return Result($"流程 {modelKey} 有 {versions.Count} 个版本", payload, Evidence(("modelKey", modelKey), ("versionCount", versions.Count)));
    }
}
