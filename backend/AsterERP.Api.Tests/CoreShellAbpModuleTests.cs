using AsterERP.Api.Infrastructure.Abp.CoreShell;
using AsterERP.Api.Application.Auth;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class CoreShellAbpModuleTests
{
    [Fact]
    public void CoreShell_is_owned_by_abp()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpCoreShellModule)));
    }

    [Fact]
    public void CoreShell_abp_module_registers_real_application_services_and_migrator()
    {
        var services = new ServiceCollection();
        var module = new AsterErpCoreShellModule();

        module.ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAuthService) &&
            descriptor.ImplementationType == typeof(AuthService));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IWorkspaceTransitionService) &&
            descriptor.ImplementationType == typeof(WorkspaceTransitionService));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(CoreShellSchemaMigrator));
    }
}
