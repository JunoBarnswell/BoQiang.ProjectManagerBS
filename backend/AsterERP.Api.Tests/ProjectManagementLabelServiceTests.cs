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
    public async Task Labels_are_project_scoped_and_task_assignment_is_versioned()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-label-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        var service = new ProjectManagementLabelService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var label = await service.CreateAsync("project-a", new ProjectManagementLabelUpsertRequest("风险", "#FF0000"));
        var task = await service.QueryTaskLabelsAsync("task-a");
        Assert.Empty(task);
        await service.SetTaskLabelsAsync("task-a", new ProjectManagementTaskLabelSetRequest(new[] { label.Id }, 1));
        Assert.Equal("风险", Assert.Single(await service.QueryTaskLabelsAsync("task-a")).LabelName);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.DeleteAsync("project-a", label.Id, label.VersionNo));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.SetTaskLabelsAsync("task-a", new ProjectManagementTaskLabelSetRequest([], 1)));
    }

    [Fact]
    public void Label_controllers_require_view_and_manage_permissions()
    {
        Assert.Contains(typeof(ProjectManagementLabelsController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementLabelView);
        Assert.Contains(typeof(ProjectManagementLabelsController).GetMethod(nameof(ProjectManagementLabelsController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementLabelManage);
        Assert.Contains(typeof(ProjectManagementTaskLabelsController).GetMethod(nameof(ProjectManagementTaskLabelsController.SetAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskEdit);
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "MES"), new Claim(AsterErpClaimTypes.DataScope, "SELF") }, "test")));
    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
