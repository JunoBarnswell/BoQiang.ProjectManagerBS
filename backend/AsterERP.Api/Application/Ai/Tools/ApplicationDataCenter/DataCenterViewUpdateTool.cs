using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterViewUpdateTool(ApplicationDataSourceViewWorkbenchService viewService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.ViewUpdate,
        "编辑数据库视图",
        "重建数据库视图并更新别名、备注和 SQL",
        "L4",
        AiDataCenterToolDefinition.AiWritePermission,
        PermissionCodes.AppDataCenterQueryDatasetEdit,
        ["Agent"],
        ["dataSourceId", "viewId", "request"],
        requiresConfirmation: true))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var viewId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "viewId");
        var request = AiDataCenterArgumentReader.ReadRequest<ApplicationDataSourceViewUpsertRequest>(context.Arguments, "request");
        var view = await viewService.UpdateAsync(dataSourceId, viewId, request, cancellationToken);
        return Result($"已更新视图 {view.ViewName}", view);
    }
}
