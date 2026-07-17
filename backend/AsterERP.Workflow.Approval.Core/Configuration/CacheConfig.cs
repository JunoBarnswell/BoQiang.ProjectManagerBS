using Microsoft.Extensions.DependencyInjection;
using AsterERP.Workflow.Approval.Core.Caching;

namespace AsterERP.Workflow.Approval.Core.Configuration;

public static class CacheConfig
{
    public static IServiceCollection AddAsterERPWorkflowApprovalCaching(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton(typeof(CustomDeploymentCache<>));
        return services;
    }
}
