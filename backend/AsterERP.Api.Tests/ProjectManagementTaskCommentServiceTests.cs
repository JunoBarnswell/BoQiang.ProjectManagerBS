using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskCommentServiceTests
{
    [Fact]
    public async Task Comments_support_threading_concurrency_and_safe_markdown()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-comments-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        db.CodeFirst.InitTables<SystemUserEntity>();
        await db.Insertable(new SystemUserEntity { Id = "user-a", UserName = "alice", DisplayName = "Alice", Status = "Enabled" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", UserId = "user-a", RoleCode = "Member", IsActive = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        var realtime = new RecordingRealtimePublisher();
        var service = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), realtimePublisher: realtime);
        var root = await service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("**root**", MentionUserIds: ["user-a"]));
        var reply = await service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("reply", root.Id));
        Assert.Equal(root.Id, reply.ParentCommentId);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("<script>alert(1)</script>")));
        var edited = await service.UpdateAsync("task-a", root.Id, new ProjectManagementTaskCommentUpsertRequest("edited", VersionNo: root.VersionNo));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync("task-a", root.Id, new ProjectManagementTaskCommentUpsertRequest("stale", VersionNo: root.VersionNo)));
        await service.DeleteAsync("task-a", reply.Id, reply.VersionNo);
        Assert.Single(await service.QueryAsync("task-a"));
        Assert.Equal("edited", edited.Markdown);
        var mention = Assert.Single(root.Mentions);
        Assert.Equal("user-a", mention.UserId);
        Assert.Equal("Alice", mention.DisplayName);
        Assert.Equal(["comment.created", "comment.created", "comment.updated", "comment.deleted"], realtime.Events);
    }

    [Fact]
    public async Task Comment_query_requires_project_visibility_and_failed_activity_rolls_back_comment()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-comment-security-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "owner" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "owner", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();

        var outsider = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser("outsider"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => outsider.QueryAsync("task-a"));

        var owner = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser("owner"), activityWriter: new ThrowingActivityWriter());
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("rollback")));
        Assert.False(await db.Queryable<ProjectManagementTaskCommentEntity>().AnyAsync(item => item.TaskId == "task-a"));
    }

    [Fact]
    public async Task Mention_candidates_are_project_member_scoped_enabled_and_paged()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-comment-mentions-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        db.CodeFirst.InitTables<SystemUserEntity>();
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new SystemUserEntity { Id = "user-a", UserName = "alice", DisplayName = "Alice 项目成员", Status = "Enabled" },
            new SystemUserEntity { Id = "user-b", UserName = "bob", DisplayName = "Bob 已停用", Status = "Disabled" },
            new SystemUserEntity { Id = "user-c", UserName = "carol", DisplayName = "Carol 非成员", Status = "Enabled" },
            new SystemUserEntity { Id = "user-d", UserName = "david", DisplayName = "David 其他工作区", Status = "Enabled" },
            new SystemUserEntity { Id = "user-e", UserName = "aaron", DisplayName = "Aaron 项目成员", Status = "Enabled" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementProjectMemberEntity { Id = "member-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", UserId = "user-a", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-b", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", UserId = "user-b", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-d", TenantId = "tenant-b", AppCode = "MES", ProjectId = "project-a", UserId = "user-d", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-e", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", UserId = "user-e", IsActive = true }
        }).ExecuteCommandAsync();

        var service = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var paged = await service.QueryMentionCandidatesAsync("task-a", new ProjectManagementTaskCommentMentionCandidateQuery(PageSize: 1));
        var result = await service.QueryMentionCandidatesAsync("task-a", new ProjectManagementTaskCommentMentionCandidateQuery("Alice", PageIndex: 0, PageSize: 200));

        Assert.Equal(2, paged.Total);
        Assert.Single(paged.Items);
        Assert.Equal("user-e", paged.Items[0].UserId);
        Assert.Equal(1, result.Total);
        var candidate = Assert.Single(result.Items);
        Assert.Equal("user-a", candidate.UserId);
        Assert.Equal("alice", candidate.UserName);
        Assert.Equal("Alice 项目成员", candidate.DisplayName);
        var outsider = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser("outsider"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => outsider.QueryMentionCandidatesAsync("task-a", new ProjectManagementTaskCommentMentionCandidateQuery()));
    }

    [Fact]
    public void Comment_controller_separates_view_and_manage_permissions()
    {
        Assert.Contains(typeof(ProjectManagementTaskCommentsController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementCommentView);
        Assert.Contains(typeof(ProjectManagementTaskCommentsController).GetMethod(nameof(ProjectManagementTaskCommentsController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementCommentAdd);
    }

    private static FixedAsterErpCurrentUser CreateUser(string userId = "operator") => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "MES")
    }, "test")));

    private sealed class ThrowingActivityWriter : IProjectManagementActivityWriter
    {
        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default) => throw new InvalidOperationException("activity failure");
    }

    private sealed class RecordingRealtimePublisher : IProjectManagementRealtimePublisher
    {
        public List<string> Events { get; } = [];
        public Task PublishInvalidationAsync(ProjectManagementDataInvalidationEvent invalidation, CancellationToken cancellationToken = default)
        {
            Events.Add(invalidation.EventType);
            return Task.CompletedTask;
        }
    }
    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
