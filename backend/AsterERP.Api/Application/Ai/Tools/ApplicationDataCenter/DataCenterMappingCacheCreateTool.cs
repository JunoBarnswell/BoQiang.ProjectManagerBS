using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterMappingCacheCreateTool(ApplicationMappingCacheWorkbenchService mappingCacheService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.MappingCacheCreate,
        "创建映射缓存",
        "创建可测试和刷新的映射缓存配置",
        "L3",
        AiDataCenterToolDefinition.AiWritePermission,
        PermissionCodes.AppDataCenterMappingCacheAdd,
        ["Agent"],
        ["dataSourceId", "request"],
        requiresConfirmation: true))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var request = AiDataCenterArgumentReader.ReadRequest<ApplicationMappingCacheUpsertRequest>(context.Arguments, "request");
        var cache = await mappingCacheService.CreateAsync(dataSourceId, request, cancellationToken);
        return Result($"已创建映射缓存 {cache.CacheName}", cache);
    }
}
