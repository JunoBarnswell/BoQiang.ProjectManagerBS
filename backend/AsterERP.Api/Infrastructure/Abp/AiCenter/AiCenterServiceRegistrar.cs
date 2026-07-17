using AsterERP.Api.Application.Ai.Tools.SystemAdministration;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterServiceRegistrar
{
    public static void RegisterApplicationServices(IServiceCollection services)
    {
        AiCenterApplicationServiceRegistrar.Register(services);
        AiCenterFlowiseServiceRegistrar.Register(services);
        AiCenterKernelFunctionRegistrar.Register(services);
        AiCenterWorkflowToolRegistrar.Register(services);
        AiCenterDataCenterToolRegistrar.Register(services);
        SystemAdminToolRegistration.RegisterSystemAdministrationTools(services);
    }

    public static void RegisterInfrastructureServices(IServiceCollection services)
    {
        AiCenterInfrastructureServiceRegistrar.Register(services);
    }
}
