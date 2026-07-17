using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.Approval.Core.Configuration.Modeler;

public static class ModelerBeanConfig
{
    public static IServiceCollection AddAsterERPFlowModeler(this IServiceCollection services)
    {
        return services;
    }
}
