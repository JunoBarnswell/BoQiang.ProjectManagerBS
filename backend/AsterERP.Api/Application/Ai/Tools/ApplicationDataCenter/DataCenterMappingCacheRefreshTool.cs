using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterMappingCacheRefreshTool(ApplicationMappingCacheWorkbenchService mappingCacheService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.MappingCacheRefresh,
        "刷新映射缓存",
        "真实执行映射缓存 SQL 并更新缓存刷新状态",
        "L3",
        AiDataCenterToolDefinition.AiOperatePermission,
        PermissionCodes.AppDataCenterMappingCacheRefresh,
        ["Agent"],
        ["dataSourceId", "cacheId"],
        requiresConfirmation: true))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var cacheId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "cacheId");
        var result = await mappingCacheService.RefreshAsync(dataSourceId, cacheId, cancellationToken);
        return Result(result.Success ? $"已刷新 {result.RowCount} 行映射缓存" : "映射缓存刷新失败", result);
    }
}
