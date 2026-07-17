using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterViewPreviewTool(ApplicationDataSourceViewWorkbenchService viewService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.ViewPreview,
        "预览视图 SQL",
        "校验并预览 SELECT SQL 返回字段和样例数据",
        "L2",
        AiDataCenterToolDefinition.AiReadPermission,
        PermissionCodes.AppDataCenterQueryDatasetPreview,
        ["Ask", "Plan", "Agent"],
        ["dataSourceId", "sql"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var sql = AiDataCenterArgumentReader.RequiredString(context.Arguments, "sql");
        var maxRows = Math.Clamp(AiDataCenterArgumentReader.ReadInt(context.Arguments, "maxRows", 20), 1, 200);
        var preview = await viewService.PreviewSqlAsync(dataSourceId, new ApplicationDataSourceSqlPreviewRequest(sql, maxRows), cancellationToken);
        return Result($"预览返回 {preview.Rows.Count} 行", preview);
    }
}
