using AsterERP.Api.Application.Auth;
using AsterERP.Api.Application.Echo;
using AsterERP.Api.Application.Health;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.CoreShell;

[DependsOn(typeof(AbpAspNetCoreMvcModule))]
public sealed class AsterErpCoreShellModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<HealthService>();
        context.Services.AddSingleton<EchoService>();
        context.Services.AddSingleton<ApplicationLoginBootstrapCache>();
        context.Services.AddScoped<IAuthService, AuthService>();
        context.Services.AddScoped<IApplicationAuthService, ApplicationAuthService>();
        context.Services.AddScoped<IWorkspaceTransitionService, WorkspaceTransitionService>();
        context.Services.AddScoped<ApplicationWorkspaceUserResolver>();
        context.Services.AddScoped<IWorkspaceMenuReader, WorkspaceMenuReader>();
        context.Services.AddScoped<ILoginLogWriter, LoginLogWriter>();
        context.Services.AddScoped<CoreShellSchemaMigrator>();
    }
}
