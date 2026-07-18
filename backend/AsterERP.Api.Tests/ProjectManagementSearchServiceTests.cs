using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementSearchServiceTests
{
    [Fact]
    public async Task Search_groups_projects_tasks_and_comments_with_bounded_results()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-search-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "Alpha", Description = "search target", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "H", ProjectName = "Hidden search target", Description = "search target", OwnerUserId = "another-user" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Search Task", Description = "search target", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", TaskCode = "T-H", Title = "Hidden Search Task", Description = "search target", CreatedBy = "another-user", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskCommentEntity { Id = "comment-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", Markdown = "search target comment", AuthorUserId = "operator", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskCommentEntity { Id = "comment-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", TaskId = "task-hidden", Markdown = "search target hidden comment", AuthorUserId = "another-user", CreatedBy = "another-user", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementProjectEntity), CreateUser(), "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), CreateUser(), "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskCommentEntity), CreateUser(), "tenant-a", "SYSTEM"));
        var service = new ProjectManagementSearchService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var result = await service.SearchAsync(new ProjectManagementSearchQuery("search", Limit: 1));
        Assert.Single(result.Projects); Assert.Single(result.Tasks); Assert.Single(result.Comments);
        Assert.Contains("search", result.Tasks[0].Summary!, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.SearchAsync(new ProjectManagementSearchQuery(" ")));
    }

    [Fact]
    public async Task Search_applies_structured_scope_project_status_time_and_page_filters()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-search-filters-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var now = DateTime.UtcNow;
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "project-active", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "ACTIVE", ProjectName = "Search Active", Status = "Active", OwnerUserId = "operator", CreatedTime = now.AddMinutes(-2) },
            new ProjectManagementProjectEntity { Id = "project-paused", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "PAUSED", ProjectName = "Search Paused", Status = "Paused", OwnerUserId = "operator", CreatedTime = now.AddDays(-2) }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "task-active", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-active", TaskCode = "A-1", Title = "Search task active", Status = "InProgress", CreatedTime = now.AddMinutes(-1) },
            new ProjectManagementTaskEntity { Id = "task-paused", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-paused", TaskCode = "P-1", Title = "Search task paused", Status = "Todo", CreatedTime = now.AddDays(-2) }
        }).ExecuteCommandAsync();
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementProjectEntity), CreateUser(), "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), CreateUser(), "tenant-a", "SYSTEM"));
        var service = new ProjectManagementSearchService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        var result = await service.SearchAsync(new ProjectManagementSearchQuery(
            "Search", Scope: "tasks", Limit: 1, ProjectId: "project-active", Status: "InProgress", From: now.AddHours(-1), To: now, PageIndex: 1));

        var task = Assert.Single(result.Tasks);
        Assert.Equal("task-active", task.Id);
        Assert.Empty(result.Projects);
        Assert.Empty(result.Comments);
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
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
