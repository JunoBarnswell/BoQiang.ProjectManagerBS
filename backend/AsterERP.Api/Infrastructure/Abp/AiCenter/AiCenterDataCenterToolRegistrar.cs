using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterDataCenterToolRegistrar
{
    public static void Register(IServiceCollection services)
    {
        services.AddScoped<IAiKernelFunction, DataCenterTableSearchTool>();
        services.AddScoped<IAiKernelFunction, DataCenterTableDescribeTool>();
        services.AddScoped<IAiKernelFunction, DataCenterTableCreateTool>();
        services.AddScoped<IAiKernelFunction, DataCenterTableQueryRowsTool>();
        services.AddScoped<IAiKernelFunction, DataCenterTableInsertRowTool>();
        services.AddScoped<IAiKernelFunction, DataCenterTableUpdateRowTool>();
        services.AddScoped<IAiKernelFunction, DataCenterTableDeleteRowTool>();
        services.AddScoped<IAiKernelFunction, DataCenterViewCreateTool>();
        services.AddScoped<IAiKernelFunction, DataCenterViewUpdateTool>();
        services.AddScoped<IAiKernelFunction, DataCenterViewPreviewTool>();
        services.AddScoped<IAiKernelFunction, DataCenterMappingCacheCreateTool>();
        services.AddScoped<IAiKernelFunction, DataCenterMappingCacheRefreshTool>();
    }
}
