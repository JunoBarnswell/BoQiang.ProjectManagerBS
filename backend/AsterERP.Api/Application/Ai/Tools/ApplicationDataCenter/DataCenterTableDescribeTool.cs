using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterTableDescribeTool(ApplicationDataSourceTableWorkbenchService tableService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.TableDescribe,
        "查看表结构",
        "读取指定数据表的字段、类型、主键和可空信息",
        "L1",
        AiDataCenterToolDefinition.AiReadPermission,
        PermissionCodes.AppDataCenterDataSourceView,
        ["Ask", "Plan", "Agent"],
        ["dataSourceId", "tableName"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var tableName = AiDataCenterArgumentReader.RequiredString(context.Arguments, "tableName");
        var table = await tableService.GetTableAsync(dataSourceId, tableName, cancellationToken);
        return Result($"读取到 {table.Columns.Count} 个字段", table);
    }
}
