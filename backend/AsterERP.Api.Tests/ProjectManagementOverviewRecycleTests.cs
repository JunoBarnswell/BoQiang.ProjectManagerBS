using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementOverviewRecycleTests
{
    [Fact]
    public async Task My_work_uses_orm_filters_and_aggregates_assignee_participant_and_mention()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "project-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "VISIBLE", ProjectName = "Visible", OwnerUserId = "owner" },
            new ProjectManagementProjectEntity { Id = "project-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "HIDDEN", ProjectName = "Hidden", OwnerUserId = "other" }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", UserId = "operator", RoleCode = "Member", IsActive = true }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "assigned", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", TaskCode = "A", Title = "assigned", AssigneeUserId = "operator", Status = "Todo" },
            new ProjectManagementTaskEntity { Id = "participant", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", TaskCode = "P", Title = "participant", Status = "Todo" },
            new ProjectManagementTaskEntity { Id = "mentioned", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", TaskCode = "M", Title = "mentioned", Status = "Todo" },
            new ProjectManagementTaskEntity { Id = "hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", TaskCode = "X", Title = "hidden", AssigneeUserId = "operator", Status = "Todo" }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskParticipantEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", TaskId = "participant", UserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskCommentEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", TaskId = "mentioned", AuthorUserId = "owner", Markdown = "@operator", MentionUserIdsJson = "[\"operator\"]" }).ExecuteCommandAsync();

        var user = CreateUser("operator");
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementProjectEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskParticipantEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskCommentEntity), user, "tenant-a", "SYSTEM"));

        var result = await new ProjectManagementMyWorkService(new TestWorkspaceDatabaseAccessor(db), user).QueryAsync(new ProjectManagementMyWorkQuery());

        Assert.Equal(3, result.Total);
        Assert.DoesNotContain(result.Items, item => item.Task.Id == "hidden");
        Assert.True(Assert.Single(result.Items, item => item.Task.Id == "assigned").IsAssignee);
        Assert.True(Assert.Single(result.Items, item => item.Task.Id == "participant").IsParticipant);
        Assert.True(Assert.Single(result.Items, item => item.Task.Id == "mentioned").IsMentioned);
    }

    [Fact]
    public async Task Recycle_query_is_limited_to_current_workspace_and_member_projects()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "deleted-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "owner", IsDeleted = true },
            new ProjectManagementProjectEntity { Id = "deleted-other", TenantId = "tenant-b", AppCode = "SYSTEM", ProjectCode = "B", ProjectName = "B", OwnerUserId = "operator", IsDeleted = true }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "deleted-visible", UserId = "operator", RoleCode = "Manager" }).ExecuteCommandAsync();

        var user = CreateUser("operator");
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementProjectEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), user, "tenant-a", "SYSTEM"));
        var service = new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), user);
        var result = await service.QueryAsync(new ProjectManagementRecycleQuery());

        Assert.Single(result.Projects.Items);
        Assert.Equal("deleted-visible", result.Projects.Items[0].Id);
    }

    [Fact]
    public async Task Purge_project_is_blocked_when_tasks_still_reference_it()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "deleted-project", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", IsDeleted = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "deleted-project", TaskCode = "T-1", Title = "task", IsDeleted = true }).ExecuteCommandAsync();

        var service = new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator", PermissionCodes.ProjectManagementProjectPurge));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.PurgeProjectAsync("deleted-project", new ProjectManagementRecyclePurgeRequest(1, "secret", true)));
    }

    [Fact]
    public async Task Purge_preview_is_blocked_by_project_scoped_history_reference()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "deleted-project", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", IsDeleted = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementActivityEntity
        {
            TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "deleted-project", AggregateType = "Project", AggregateId = "deleted-project",
            ActivityType = "deleted", TraceId = "trace", ActorUserId = "operator"
        }).ExecuteCommandAsync();

        var service = new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator"));
        var preview = await service.PreviewPurgeProjectAsync("deleted-project", 1);

        Assert.False(preview.CanExecute);
        Assert.Equal(0, preview.MemberReferenceCount);
        Assert.Equal(0, preview.MilestoneReferenceCount);
        Assert.Equal(0, preview.TaskReferenceCount);
        Assert.Contains("关联记录", preview.BlockingReason);
    }

    [Fact]
    public async Task Restore_task_is_blocked_until_its_project_is_restored()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "deleted-project", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", IsDeleted = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "deleted-task", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "deleted-project", TaskCode = "T-1", Title = "task", IsDeleted = true }).ExecuteCommandAsync();

        var service = new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator"));

        var exception = await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RestoreTaskAsync("deleted-task", new ProjectManagementRecycleRestoreRequest(1)));

        Assert.Contains("所属项目已删除", exception.Message);
        Assert.True((await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "deleted-task").SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Restore_project_rolls_back_when_progress_projection_fails()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "deleted-project", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", IsDeleted = true }).ExecuteCommandAsync();

        var service = new ProjectManagementRecycleService(
            new TestWorkspaceDatabaseAccessor(db),
            CreateUser("operator"),
            progressProjector: new FailingProgressProjector());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreProjectAsync("deleted-project", new ProjectManagementRecycleRestoreRequest(1)));

        var restored = await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == "deleted-project").SingleAsync();
        Assert.True(restored.IsDeleted);
        Assert.Equal(1, restored.VersionNo);
    }

    [Fact]
    public async Task Restore_task_with_descendants_restores_the_entire_deleted_subtree()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "root", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "root", IsDeleted = true, VersionNo = 2 },
            new ProjectManagementTaskEntity { Id = "child", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", ParentTaskId = "root", TaskCode = "T-2", Title = "child", IsDeleted = true, VersionNo = 2 }
        }).ExecuteCommandAsync();

        var service = new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator"));
        await service.RestoreTaskAsync("root", new ProjectManagementRecycleRestoreRequest(2, RestoreDescendants: true));

        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == "project-a").ToListAsync();
        Assert.All(tasks, task => Assert.False(task.IsDeleted));
        Assert.All(tasks, task => Assert.Equal(3, task.VersionNo));
    }

    private static SqlSugarClient CreateDb() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:overview-recycle-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser(string userId, string? permission = null) => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        permission is null ? new Claim("unused", "unused") : new Claim(AsterErpClaimTypes.PermissionCode, permission)
    }, "test")));

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class FailingProgressProjector : IProjectManagementTaskProgressProjector
    {
        public Task RefreshAsync(string projectId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("projection failure");
    }
}
