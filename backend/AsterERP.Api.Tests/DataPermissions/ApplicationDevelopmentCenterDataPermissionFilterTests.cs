using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using Xunit;

namespace AsterERP.Api.Tests.DataPermissions;

public sealed class ApplicationDevelopmentCenterDataPermissionFilterTests
{
    [Fact]
    public void Every_registered_entity_has_a_tenant_and_app_code_query_filter_branch()
    {
        var registry = new DataPermissionFilterRegistry();
        AsterErpApplicationDevelopmentCenterModule.RegisterDataFilters(registry);
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "backend", "AsterERP.Api", "Infrastructure", "Security", "DataPermissions", "DataPermissionFilterRegistrar.cs"));

        foreach (var entityType in registry.WorkspaceEntityTypes)
        {
            if (entityType.Namespace != typeof(ApplicationDevelopmentVersionEntity).Namespace) continue;

            Assert.NotNull(entityType.GetProperty(nameof(ApplicationDevelopmentVersionEntity.TenantId)));
            Assert.NotNull(entityType.GetProperty(nameof(ApplicationDevelopmentVersionEntity.AppCode)));
            Assert.Contains($"if (entityType == typeof({entityType.Name}))", source, StringComparison.Ordinal);
            Assert.Contains($"Db.QueryFilter.AddTableFilter<{entityType.Name}>(item => item.TenantId == tenantId && item.AppCode == appCode);", source, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("AsterERP.sln was not found");
    }
}
