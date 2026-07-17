using AsterERP.Api.Application.Ai;
using AsterERP.Api.Infrastructure.Ai;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.SemanticKernel;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterInfrastructureServiceRegistrar
{
    public static void Register(IServiceCollection services)
    {
        services.AddDataProtection();
        services.AddKernel();
        services.AddScoped<IFunctionInvocationFilter, AiKernelFunctionInvocationFilter>();
        services.AddScoped<IAutoFunctionInvocationFilter, AiKernelAutoFunctionInvocationFilter>();
        services.AddScoped<IPromptRenderFilter, AiKernelPromptRenderFilter>();
        services.AddSingleton<AiRunCancellationRegistry>();
        services.AddSingleton<IAiSecretProtector, AiSecretProtector>();
        services.AddScoped<IAiModelRouter, AiModelRouter>();
        services.AddScoped<AiKernelChatRuntime>();
        services.AddScoped<SseEventWriter>();
    }
}
