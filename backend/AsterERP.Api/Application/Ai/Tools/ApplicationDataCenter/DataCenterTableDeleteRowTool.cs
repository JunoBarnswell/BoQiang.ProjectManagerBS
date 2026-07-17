using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterTableDeleteRowTool(ApplicationDataSourceTableRowService rowService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.TableDeleteRow,
        "删除表数据",
        "按主键删除指定数据表的一行数据",
        "L4",
        AiDataCenterToolDefinition.AiWritePermission,
        PermissionCodes.AppDataCenterDataSourceDataEdit,
        ["Agent"],
        ["dataSourceId", "tableName", "keyValues", "originalValues", "confirmed", "expectedAffectedRows"],
        requiresConfirmation: true))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var tableName = AiDataCenterArgumentReader.RequiredString(context.Arguments, "tableName");
        var keyValues = AiDataCenterArgumentReader.ReadDictionary(context.Arguments, "keyValues");
        var originalValues = AiDataCenterArgumentReader.ReadDictionary(context.Arguments, "originalValues");
        context.Arguments.TryGetValue("versionValue", out var versionValue);
        var confirmed = AiDataCenterArgumentReader.ReadBool(context.Arguments, "confirmed", false);
        var expectedAffectedRows = AiDataCenterArgumentReader.ReadNullableInt(context.Arguments, "expectedAffectedRows");
        var request = new ApplicationDataSourceTableRowDeleteRequest
        {
            Confirmed = confirmed,
            ConflictResolution = AiDataCenterArgumentReader.ReadString(context.Arguments, "conflictResolution"),
            ExpectedAffectedRows = expectedAffectedRows,
            AuditId = AiDataCenterArgumentReader.ReadString(context.Arguments, "auditId"),
            KeyValues = new Dictionary<string, object?>(keyValues, StringComparer.OrdinalIgnoreCase),
            OriginalValues = new Dictionary<string, object?>(originalValues, StringComparer.OrdinalIgnoreCase),
            RequestHash = AiDataCenterArgumentReader.ReadString(context.Arguments, "requestHash"),
            VersionValue = versionValue
        };
        var mutation = await rowService.DeleteRowAsync(dataSourceId, tableName, request, cancellationToken);
        return Result("已删除表数据", mutation);
    }
}
