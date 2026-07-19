using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskDependencyServiceTests
{
    [Fact]
    public async Task Dependencies_reject_self_links_duplicate_links_and_cycles()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var tasks = new[]
        {
            new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "A", Title = "A", CreatedBy = "operator", CreatedTime = DateTime.UtcNow },
            new ProjectManagementTaskEntity { Id = "task-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "B", Title = "B", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }
        };
        await db.Insertable(tasks).ExecuteCommandAsync();
        var service = new ProjectManagementTaskDependencyService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var created = await service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-a", "task-b"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-a", "task-b")));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-b", "task-a")));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementTaskDependencyUpsertRequest("task-a", "task-a")));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.DeleteAsync("project-a", created.Id, created.VersionNo - 1));
        await service.DeleteAsync("project-a", created.Id, created.VersionNo);
        Assert.Empty(await service.QueryAsync("project-a"));
    }

    [Fact]
    public void Dependency_controller_requires_view_and_dependency_manage_permissions()
    {
        Assert.Contains(typeof(ProjectManagementTaskDependenciesController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskView);
        Assert.Contains(typeof(ProjectManagementTaskDependenciesController).GetMethod(nameof(ProjectManagementTaskDependenciesController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskManageDependency);
        Assert.Contains(typeof(ProjectManagementTaskDependenciesController).GetMethod(nameof(ProjectManagementTaskDependenciesController.DeleteAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskManageDependency);
    }

    private static SqlSugarClient CreateDb() => new(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-dependencies-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"), new Claim(AsterErpClaimTypes.DataScope, "SELF")
    }, "test")));
    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
