using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterViewCreateTool(ApplicationDataSourceViewWorkbenchService viewService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.ViewCreate,
        "创建数据库视图",
        "真实执行 SQL 创建数据库视图并保存工作台元数据",
        "L4",
        AiDataCenterToolDefinition.AiWritePermission,
        PermissionCodes.AppDataCenterQueryDatasetAdd,
        ["Agent"],
        ["dataSourceId", "request"],
        requiresConfirmation: true))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var request = AiDataCenterArgumentReader.ReadRequest<ApplicationDataSourceViewUpsertRequest>(context.Arguments, "request");
        var view = await viewService.CreateAsync(dataSourceId, request, cancellationToken);
        return Result($"已创建视图 {view.ViewName}", view);
    }
}
