using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterTableQueryRowsTool(ApplicationDataSourceTableRowService rowService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.TableQueryRows,
        "查询表数据",
        "分页查询指定数据表的真实数据",
        "L2",
        AiDataCenterToolDefinition.AiReadPermission,
        PermissionCodes.AppDataCenterDataSourceDataQuery,
        ["Ask", "Plan", "Agent"],
        ["dataSourceId", "tableName"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var tableName = AiDataCenterArgumentReader.RequiredString(context.Arguments, "tableName");
        var request = AiDataCenterArgumentReader.TryReadRequest<ApplicationDataSourceTableRowsQueryRequest>(context.Arguments, "request")
                      ?? new ApplicationDataSourceTableRowsQueryRequest();
        if (request.PageSize <= 0)
        {
            request.PageSize = Math.Clamp(AiDataCenterArgumentReader.ReadInt(context.Arguments, "pageSize", 20), 1, 200);
        }

        request.Keyword ??= AiDataCenterArgumentReader.ReadString(context.Arguments, "keyword");
        var rows = await rowService.QueryRowsAsync(dataSourceId, tableName, request, cancellationToken);
        return Result($"查询到 {rows.Total} 行，当前返回 {rows.Rows.Count} 行", rows);
    }
}
