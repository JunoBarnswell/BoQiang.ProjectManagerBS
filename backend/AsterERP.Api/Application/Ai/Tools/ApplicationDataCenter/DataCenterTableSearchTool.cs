using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterTableSearchTool(ApplicationDataSourceTableWorkbenchService tableService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.TableSearch,
        "查询数据表",
        "查询当前数据源下的数据表和视图清单",
        "L1",
        AiDataCenterToolDefinition.AiReadPermission,
        PermissionCodes.AppDataCenterDataSourceView,
        ["Ask", "Plan", "Agent"],
        ["dataSourceId"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var keyword = AiDataCenterArgumentReader.ReadString(context.Arguments, "keyword");
        var tables = await tableService.GetTablesAsync(dataSourceId, cancellationToken);
        var filtered = (string.IsNullOrWhiteSpace(keyword)
            ? tables
            : tables.Where(item =>
                item.TableName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (item.SchemaName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToArray()).ToArray();
        return Result($"查询到 {filtered.Length} 个数据表对象", new { dataSourceId, items = filtered });
    }
}
