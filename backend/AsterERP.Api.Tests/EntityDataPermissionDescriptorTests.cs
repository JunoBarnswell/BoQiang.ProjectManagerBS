using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Modules.ApplicationDataCenter;
using Microsoft.AspNetCore.Http;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class EntityDataPermissionDescriptorTests
{
    [Fact]
    public async Task RoleDescriptor_OnlyAllowsCurrentTenantAndWorkspace()
    {
        var descriptor = new SystemRoleDataPermissionDescriptor(CreateCurrentUser());
        var filter = await descriptor.BuildAsync();
        Assert.NotNull(filter);
        var predicate = filter!.Compile();

        Assert.True(predicate(new SystemRoleEntity { TenantId = "tenant-a", AppCode = "MES" }));
        Assert.False(predicate(new SystemRoleEntity { TenantId = "tenant-b", AppCode = "MES" }));
        Assert.False(predicate(new SystemRoleEntity { TenantId = "tenant-a", AppCode = "WMS" }));
    }

    [Fact]
    public async Task MenuDescriptor_OnlyAllowsCurrentTenantAndWorkspace()
    {
        var descriptor = new SystemMenuDataPermissionDescriptor(CreateCurrentUser());
        var filter = await descriptor.BuildAsync();
        Assert.NotNull(filter);
        var predicate = filter!.Compile();

        Assert.True(predicate(new SystemMenuEntity { TenantId = "tenant-a", AppCode = "MES" }));
        Assert.False(predicate(new SystemMenuEntity { TenantId = "tenant-b", AppCode = "MES" }));
        Assert.False(predicate(new SystemMenuEntity { TenantId = "tenant-a", AppCode = "WMS" }));
    }

    [Fact]
    public async Task UserDescriptor_OnlyAllowsSelfForSelfScope()
    {
        var descriptor = new SystemUserDataPermissionDescriptor(CreateCurrentUser("SELF"), new ThrowingDepartmentResolver());
        var filter = await descriptor.BuildAsync();
        Assert.NotNull(filter);
        var predicate = filter!.Compile();

        Assert.True(predicate(new SystemUserEntity { Id = "user-a" }));
        Assert.False(predicate(new SystemUserEntity { Id = "user-b" }));
    }

    [Fact]
    public async Task ApplicationDataCenterDescriptors_OnlyAllowCurrentTenantAndWorkspace()
    {
        var currentUser = CreateCurrentUser();
        var dataSourceFilter = (await new ApplicationDataSourceDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();
        var datasetFilter = (await new ApplicationQueryDatasetDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();

        Assert.True(dataSourceFilter(new ApplicationDataSourceEntity { TenantId = "tenant-a", AppCode = "MES" }));
        Assert.False(dataSourceFilter(new ApplicationDataSourceEntity { TenantId = "tenant-b", AppCode = "MES" }));
        Assert.False(dataSourceFilter(new ApplicationDataSourceEntity { TenantId = "tenant-a", AppCode = "WMS" }));
        Assert.True(datasetFilter(new ApplicationQueryDatasetEntity { TenantId = "tenant-a", AppCode = "MES" }));
        Assert.False(datasetFilter(new ApplicationQueryDatasetEntity { TenantId = "tenant-b", AppCode = "MES" }));
        Assert.False(datasetFilter(new ApplicationQueryDatasetEntity { TenantId = "tenant-a", AppCode = "WMS" }));
    }

    [Fact]
    public async Task DataModelAndEntityFieldDescriptors_OnlyAllowCurrentTenantAndWorkspace()
    {
        var currentUser = CreateCurrentUser();
        var modelFilter = (await new ApplicationDataModelDesignDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();
        var entityFilter = (await new ApplicationDataEntityDefinitionDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();
        var fieldFilter = (await new ApplicationDataFieldDefinitionDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();

        AssertWorkspaceIsolation(
            modelFilter,
            new ApplicationDataModelDesignEntity(),
            item => item.TenantId = "tenant-b",
            item => item.AppCode = "WMS");
        AssertWorkspaceIsolation(
            entityFilter,
            new ApplicationDataEntityDefinitionEntity(),
            item => item.TenantId = "tenant-b",
            item => item.AppCode = "WMS");
        AssertWorkspaceIsolation(
            fieldFilter,
            new ApplicationDataFieldDefinitionEntity(),
            item => item.TenantId = "tenant-b",
            item => item.AppCode = "WMS");
    }

    [Fact]
    public async Task HighValueApplicationObjectDescriptors_OnlyAllowCurrentTenantAndWorkspace()
    {
        var currentUser = CreateCurrentUser();
        var dictionaryFilter = (await new ApplicationDataCenterDictionaryDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();
        var apiServiceFilter = (await new ApplicationApiServiceDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();
        var microflowFilter = (await new ApplicationMicroflowDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();

        AssertWorkspaceIsolation(
            dictionaryFilter,
            new ApplicationDataCenterDictionaryEntity(),
            item => item.TenantId = "tenant-b",
            item => item.AppCode = "WMS");
        AssertWorkspaceIsolation(
            apiServiceFilter,
            new ApplicationApiServiceEntity(),
            item => item.TenantId = "tenant-b",
            item => item.AppCode = "WMS");
        AssertWorkspaceIsolation(
            microflowFilter,
            new ApplicationMicroflowEntity(),
            item => item.TenantId = "tenant-b",
            item => item.AppCode = "WMS");
    }

    [Fact]
    public async Task IntegrationTaskDescriptors_OnlyAllowCurrentTenantAndWorkspace()
    {
        var currentUser = CreateCurrentUser();
        var taskFilter = (await new ApplicationIntegrationTaskDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();
        var runFilter = (await new ApplicationIntegrationTaskRunDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();

        Assert.True(taskFilter(new ApplicationIntegrationTaskEntity { TenantId = "tenant-a", AppCode = "MES" }));
        Assert.False(taskFilter(new ApplicationIntegrationTaskEntity { TenantId = "tenant-b", AppCode = "MES" }));
        Assert.False(taskFilter(new ApplicationIntegrationTaskEntity { TenantId = "tenant-a", AppCode = "WMS" }));

        Assert.True(runFilter(new ApplicationIntegrationTaskRunEntity { TenantId = "tenant-a", AppCode = "MES" }));
        Assert.False(runFilter(new ApplicationIntegrationTaskRunEntity { TenantId = "tenant-b", AppCode = "MES" }));
        Assert.False(runFilter(new ApplicationIntegrationTaskRunEntity { TenantId = "tenant-a", AppCode = "WMS" }));

    }

    [Fact]
    public async Task IntegrationTaskDescriptors_FailClosedWithoutWorkspace()
    {
        var currentUser = CreateCurrentUser(tenantId: null, appCode: null);
        var taskFilter = (await new ApplicationIntegrationTaskDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();
        var runFilter = (await new ApplicationIntegrationTaskRunDataPermissionDescriptor(currentUser).BuildAsync())!.Compile();

        Assert.False(taskFilter(new ApplicationIntegrationTaskEntity { TenantId = "tenant-a", AppCode = "MES" }));
        Assert.False(runFilter(new ApplicationIntegrationTaskRunEntity { TenantId = "tenant-a", AppCode = "MES" }));
    }

    private static void AssertWorkspaceIsolation<TEntity>(
        Func<TEntity, bool> predicate,
        TEntity allowed,
        Action<TEntity> changeTenant,
        Action<TEntity> changeWorkspace)
        where TEntity : ApplicationDataCenterObjectEntity
    {
        SetWorkspace(allowed);
        Assert.True(predicate(allowed));

        changeTenant(allowed);
        Assert.False(predicate(allowed));

        SetWorkspace(allowed);
        changeWorkspace(allowed);
        Assert.False(predicate(allowed));
    }

    private static void SetWorkspace(ApplicationDataCenterObjectEntity entity)
    {
        entity.TenantId = "tenant-a";
        entity.AppCode = "MES";
    }

    private static ICurrentUser CreateCurrentUser(
        string dataScope = "DEPT",
        string? tenantId = "tenant-a",
        string? appCode = "MES",
        IReadOnlyList<string>? permissions = null)
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "user-a", "user-a", tenantId, "Tenant A", appCode, "Tenant A MES", "dept-a",
            null, ["role-a"], ["user"], permissions ?? ["system:user:view"], dataScope, true, false, true, "User"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return new Volo.Abp.Users.CurrentUser(new HttpContextCurrentPrincipalAccessor(accessor));
    }

    private sealed class ThrowingDepartmentResolver : IDataScopeDepartmentResolver
    {
        public Task<IReadOnlyList<string>> ResolveDepartmentAndChildrenAsync(
            string? departmentId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("SELF scope must not resolve departments.");
    }
}
