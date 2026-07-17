using AsterERP.Workflow.Forms.Core.Repositories.Form;
using AsterERP.Workflow.Forms.Core.Services.Form;
using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.Forms.Core.Configuration;

public static class WorkflowFormServiceConfig
{
    public static IServiceCollection AddAsterERPWorkflowFormServices(this IServiceCollection services)
    {
        services.AddScoped<IFormInfoRepository, FormInfoRepository>();
        services.AddScoped<IFormDataInfoRepository, FormDataInfoRepository>();
        services.AddScoped<IFormInfoService, FormInfoService>();
        services.AddScoped<IFormDataInfoService, FormDataInfoService>();
        services.AddScoped<IFormFlowOperationService, FormFlowOperationService>();

        return services;
    }
}
