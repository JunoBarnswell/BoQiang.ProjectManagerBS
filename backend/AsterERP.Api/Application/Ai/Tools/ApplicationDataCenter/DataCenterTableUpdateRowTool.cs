using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterTableUpdateRowTool(ApplicationDataSourceTableRowService rowService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.TableUpdateRow,
        "编辑表数据",
        "按主键更新指定数据表的一行数据",
        "L4",
        AiDataCenterToolDefinition.AiWritePermission,
        PermissionCodes.AppDataCenterDataSourceDataEdit,
        ["Agent"],
        ["dataSourceId", "tableName", "keyValues", "values", "originalValues", "confirmed", "expectedAffectedRows"],
        requiresConfirmation: true))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var tableName = AiDataCenterArgumentReader.RequiredString(context.Arguments, "tableName");
        var keyValues = AiDataCenterArgumentReader.ReadDictionary(context.Arguments, "keyValues");
        var values = AiDataCenterArgumentReader.ReadDictionary(context.Arguments, "values");
        var originalValues = AiDataCenterArgumentReader.ReadDictionary(context.Arguments, "originalValues");
        context.Arguments.TryGetValue("versionValue", out var versionValue);
        var confirmed = AiDataCenterArgumentReader.ReadBool(context.Arguments, "confirmed", false);
        var expectedAffectedRows = AiDataCenterArgumentReader.ReadNullableInt(context.Arguments, "expectedAffectedRows");
        var request = new ApplicationDataSourceTableRowUpsertRequest
        {
            Confirmed = confirmed,
            ConflictResolution = AiDataCenterArgumentReader.ReadString(context.Arguments, "conflictResolution"),
            ExpectedAffectedRows = expectedAffectedRows,
            AuditId = AiDataCenterArgumentReader.ReadString(context.Arguments, "auditId"),
            KeyValues = new Dictionary<string, object?>(keyValues, StringComparer.OrdinalIgnoreCase),
            OriginalValues = new Dictionary<string, object?>(originalValues, StringComparer.OrdinalIgnoreCase),
            RequestHash = AiDataCenterArgumentReader.ReadString(context.Arguments, "requestHash"),
            VersionValue = versionValue,
            Values = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase)
        };
        var mutation = await rowService.UpdateRowAsync(dataSourceId, tableName, request, cancellationToken);
        return Result("已更新表数据", mutation);
    }
}
