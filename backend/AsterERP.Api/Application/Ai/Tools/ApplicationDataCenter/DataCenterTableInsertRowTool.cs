using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterTableInsertRowTool(ApplicationDataSourceTableRowService rowService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.TableInsertRow,
        "新增表数据",
        "向指定数据表新增一行数据",
        "L4",
        AiDataCenterToolDefinition.AiWritePermission,
        PermissionCodes.AppDataCenterDataSourceDataEdit,
        ["Agent"],
        ["dataSourceId", "tableName", "values", "confirmed", "expectedAffectedRows"],
        requiresConfirmation: true))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var tableName = AiDataCenterArgumentReader.RequiredString(context.Arguments, "tableName");
        var values = AiDataCenterArgumentReader.ReadDictionary(context.Arguments, "values");
        var expectedAffectedRows = AiDataCenterArgumentReader.ReadNullableInt(context.Arguments, "expectedAffectedRows");
        var confirmed = AiDataCenterArgumentReader.ReadBool(context.Arguments, "confirmed", false);
        var request = new ApplicationDataSourceTableRowUpsertRequest
        {
            AuditId = AiDataCenterArgumentReader.ReadString(context.Arguments, "auditId"),
            Confirmed = confirmed,
            ExpectedAffectedRows = expectedAffectedRows,
            RequestHash = AiDataCenterArgumentReader.ReadString(context.Arguments, "requestHash"),
            Values = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase)
        };
        var mutation = await rowService.InsertRowAsync(dataSourceId, tableName, request, cancellationToken);
        return Result("已新增表数据", mutation);
    }
}
