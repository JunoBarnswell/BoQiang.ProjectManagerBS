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

public sealed class ProjectManagementLabelServiceTests
{
    [Fact]
    public async Task Labels_support_public_and_project_scope_with_transactional_relationship_cleanup()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-label-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "project-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "B", ProjectName = "B", OwnerUserId = "operator" }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        var activities = new CapturingActivityWriter();
        var service = new ProjectManagementLabelService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), activityWriter: activities);
        var publicLabel = await service.CreatePublicAsync(new ProjectManagementLabelUpsertRequest("风险", "#ff0000"));
        var projectLabel = await service.CreateAsync("project-a", new ProjectManagementLabelUpsertRequest("风险", "#00ff00"));
        var otherProjectLabel = await service.CreateAsync("project-b", new ProjectManagementLabelUpsertRequest("风险", "#0000ff"));

        Assert.Equal(ProjectManagementLabelScopes.Public, publicLabel.Scope);
        Assert.Equal(ProjectManagementLabelScopes.Project, projectLabel.Scope);
        Assert.Equal("#FF0000", publicLabel.Color);
        Assert.Equal(2, (await service.QueryAsync("project-a")).Count);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementLabelUpsertRequest("风险")));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.SetTaskLabelsAsync("task-a", new ProjectManagementTaskLabelSetRequest([otherProjectLabel.Id], 1)));

        await service.SetTaskLabelsAsync("task-a", new ProjectManagementTaskLabelSetRequest([publicLabel.Id, projectLabel.Id], 1));
        Assert.Equal(2, (await service.QueryTaskLabelsAsync("task-a")).Count);
        var taskAfterLabels = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "task-a").FirstAsync();
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.SetTaskLabelsAsync("task-a", new ProjectManagementTaskLabelSetRequest([otherProjectLabel.Id], taskAfterLabels.VersionNo)));
        Assert.Equal(2, (await service.QueryTaskLabelsAsync("task-a")).Count);
        await service.DeleteAsync("project-a", projectLabel.Id, projectLabel.VersionNo);

        Assert.Equal("Task", (await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "task-a").FirstAsync()).Title);
        Assert.Single(await service.QueryTaskLabelsAsync("task-a"));
        Assert.Contains(activities.Events, activity => activity.ActivityType == "label.deleted" && activity.ProjectId == "project-a");

        var currentTask = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "task-a").FirstAsync();
        await service.SetTaskLabelsAsync("task-a", new ProjectManagementTaskLabelSetRequest([publicLabel.Id], currentTask.VersionNo));
        await service.DeletePublicAsync(publicLabel.Id, publicLabel.VersionNo);
        Assert.Empty(await service.QueryTaskLabelsAsync("task-a"));
        Assert.Equal(2, activities.Events.Count(activity => activity.ActivityType == "label.deleted" && activity.ProjectId == "project-a"));
    }

    [Fact]
    public void Label_controllers_require_view_and_manage_permissions()
    {
        Assert.Contains(typeof(ProjectManagementLabelsController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementLabelView);
        Assert.Contains(typeof(ProjectManagementPublicLabelsController).GetMethod(nameof(ProjectManagementPublicLabelsController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementLabelManage);
        Assert.Contains(typeof(ProjectManagementLabelsController).GetMethod(nameof(ProjectManagementLabelsController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementLabelManage);
        Assert.Contains(typeof(ProjectManagementTaskLabelsController).GetMethod(nameof(ProjectManagementTaskLabelsController.SetAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskEdit);
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"), new Claim(AsterErpClaimTypes.DataScope, "SELF") }, "test")));
    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class CapturingActivityWriter : IProjectManagementActivityWriter
    {
        public List<ProjectManagementActivityEvent> Events { get; } = [];
        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default)
        {
            Events.Add(activity);
            return Task.CompletedTask;
        }
    }
}
