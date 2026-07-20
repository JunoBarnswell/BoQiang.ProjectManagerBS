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
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "user-a", RoleCode = "Member", IsActive = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        var realtime = new RecordingRealtimePublisher();
        var service = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), realtimePublisher: realtime);
        var root = await service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("**root**", MentionUserIds: ["user-a"]));
        var reply = await service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("reply", root.Id));
        Assert.Equal(root.Id, reply.ParentCommentId);
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
    public async Task Comment_can_link_one_existing_task_attachment()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-comment-attachment-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskAttachmentEntity { Id = "attachment-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", FileId = "file-a", FileName = "note.pdf", FileSize = 10, UploadedByUserId = "operator", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();

        var service = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var comment = await service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("with attachment", AttachmentId: "attachment-a"));

        Assert.NotNull(comment.Attachment);
        Assert.Equal("attachment-a", comment.Attachment!.Id);
        Assert.Equal(comment.Id, (await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.Id == "attachment-a").SingleAsync()).CommentId);
    }

    [Fact]
    public async Task Comment_markdown_persists_only_the_safe_subset()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-comment-markdown-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();

        var service = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var result = await service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("<script>alert(1)</script> [bad](javascript:alert(1)) [good](https://example.com)"));

        Assert.DoesNotContain("<script", result.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", result.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("alert(1)", result.Markdown);
        Assert.Contains("[good](https://example.com)", result.Markdown);
    }

    [Fact]
    public async Task Comment_query_requires_project_visibility_and_failed_activity_rolls_back_comment()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-comment-security-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "owner" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "owner", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();

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
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new SystemUserEntity { Id = "operator", UserName = "operator", DisplayName = "Project Owner", Status = "Enabled" },
            new SystemUserEntity { Id = "user-a", UserName = "alice", DisplayName = "Alice 项目成员", Status = "Enabled" },
            new SystemUserEntity { Id = "user-b", UserName = "bob", DisplayName = "Bob 已停用", Status = "Disabled" },
            new SystemUserEntity { Id = "user-c", UserName = "carol", DisplayName = "Carol 非成员", Status = "Enabled" },
            new SystemUserEntity { Id = "user-d", UserName = "david", DisplayName = "David 其他工作区", Status = "Enabled" },
            new SystemUserEntity { Id = "user-e", UserName = "aaron", DisplayName = "Aaron 项目成员", Status = "Enabled" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementProjectMemberEntity { Id = "member-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "user-a", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "user-b", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-d", TenantId = "tenant-b", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "user-d", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-e", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "user-e", IsActive = true }
        }).ExecuteCommandAsync();

        var service = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var paged = await service.QueryMentionCandidatesAsync("task-a", new ProjectManagementTaskCommentMentionCandidateQuery(PageSize: 1));
        var result = await service.QueryMentionCandidatesAsync("task-a", new ProjectManagementTaskCommentMentionCandidateQuery("Alice", PageIndex: 0, PageSize: 200));
        var ownerResult = await service.QueryMentionCandidatesAsync("task-a", new ProjectManagementTaskCommentMentionCandidateQuery("Project Owner", PageIndex: 0, PageSize: 200));

        Assert.Equal(3, paged.Total);
        Assert.Single(paged.Items);
        Assert.Equal("user-e", paged.Items[0].UserId);
        Assert.Equal(1, result.Total);
        var candidate = Assert.Single(result.Items);
        Assert.Equal("user-a", candidate.UserId);
        Assert.Equal("alice", candidate.UserName);
        Assert.Equal("Alice 项目成员", candidate.DisplayName);
        var ownerCandidate = Assert.Single(ownerResult.Items);
        Assert.Equal(1, ownerResult.Total);
        Assert.Equal("operator", ownerCandidate.UserId);
        Assert.Equal("Project Owner", ownerCandidate.DisplayName);
        var outsider = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser("outsider"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => outsider.QueryMentionCandidatesAsync("task-a", new ProjectManagementTaskCommentMentionCandidateQuery()));
    }

    [Fact]
    public async Task Comment_query_is_database_paged_and_owner_can_govern_other_authors()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-comment-page-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "owner" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "operator", RoleCode = "Member", IsActive = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", AssigneeUserId = "operator", CreatedBy = "owner", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();

        var author = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator"));
        var first = await author.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("first"));
        await author.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("second"));
        await author.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("third"));

        var page = await author.QueryAsync("task-a", new ProjectManagementTaskCommentQuery(PageIndex: 2, PageSize: 2));
        Assert.Equal(3, page.Total);
        Assert.Single(page.Items);
        Assert.Equal("third", page.Items[0].Markdown);
        var newest = await author.QueryAsync("task-a", new ProjectManagementTaskCommentQuery(PageSize: 2, Sort: "desc"));
        Assert.Equal(["third", "second"], newest.Items.Select(item => item.Markdown));

        var owner = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser("owner"));
        var edited = await owner.UpdateAsync("task-a", first.Id, new ProjectManagementTaskCommentUpsertRequest("owner edit", VersionNo: first.VersionNo));
        Assert.Equal("owner edit", edited.Markdown);
        await owner.DeleteAsync("task-a", first.Id, edited.VersionNo);
        Assert.Equal(2, (await owner.QueryAsync("task-a", new ProjectManagementTaskCommentQuery(PageSize: 10))).Total);
    }

    [Fact]
    public async Task Archived_project_allows_comment_mutations()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-comment-archive-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", Status = "Archived" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();

        var service = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var created = await service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("archived project comment"));
        var updated = await service.UpdateAsync("task-a", created.Id, new ProjectManagementTaskCommentUpsertRequest("archived project comment updated", VersionNo: created.VersionNo));
        await service.DeleteAsync("task-a", updated.Id, updated.VersionNo);

        Assert.Empty(await service.QueryAsync("task-a"));
    }

    [Fact]
    public async Task Mentions_keep_snapshots_incrementally_update_and_publish_one_idempotent_notification()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-comment-mention-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        db.CodeFirst.InitTables<SystemUserEntity>();
        await db.Insertable(new[]
        {
            new SystemUserEntity { Id = "user-a", UserName = "alice", DisplayName = "Alice", Status = "Enabled" },
            new SystemUserEntity { Id = "user-b", UserName = "bob", DisplayName = "Bob Disabled", Status = "Disabled" },
            new SystemUserEntity { Id = "user-c", UserName = "carol", DisplayName = "Carol Other Project", Status = "Enabled" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "project-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "B", ProjectName = "B", OwnerUserId = "operator" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "user-a", IsActive = true },
            new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "user-b", IsActive = true },
            new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-b", UserId = "user-c", IsActive = true }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator" }).ExecuteCommandAsync();

        var notificationPublisher = new ProjectManagementNotificationService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var service = new ProjectManagementTaskCommentService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), notificationPublisher: notificationPublisher);
        var created = await service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("hello @alice", MentionUserIds: ["user-a"]));
        Assert.Equal("Alice", Assert.Single(created.Mentions).DisplayName);

        var recipientNotifications = new ProjectManagementNotificationService(new TestWorkspaceDatabaseAccessor(db), CreateUser("user-a"));
        Assert.Single((await recipientNotifications.QueryAsync(new ProjectManagementNotificationQuery())).Items);
        await notificationPublisher.PublishAsync(new ProjectManagementNotification("tenant-a", "SYSTEM", "task.comment.mentioned", "user-a", "任务评论提及", "重复", $"/projects/project-a/tasks?selectedTaskId=task-a", $"mention:{created.Id}:user-a", "project-a", "task-a"));
        Assert.Single((await recipientNotifications.QueryAsync(new ProjectManagementNotificationQuery())).Items);

        await db.Updateable<SystemUserEntity>().SetColumns(user => new SystemUserEntity { DisplayName = "Alice Renamed" }).Where(user => user.Id == "user-a").ExecuteCommandAsync();
        var snapshot = Assert.Single(await service.QueryAsync("task-a"));
        Assert.Equal("Alice", Assert.Single(snapshot.Mentions).DisplayName);

        var removed = await service.UpdateAsync("task-a", created.Id, new ProjectManagementTaskCommentUpsertRequest("removed", VersionNo: created.VersionNo));
        Assert.Empty(removed.Mentions);
        var readded = await service.UpdateAsync("task-a", created.Id, new ProjectManagementTaskCommentUpsertRequest("readded", MentionUserIds: ["user-a"], VersionNo: removed.VersionNo));
        Assert.Equal("Alice Renamed", Assert.Single(readded.Mentions).DisplayName);
        Assert.Single((await recipientNotifications.QueryAsync(new ProjectManagementNotificationQuery())).Items);

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("disabled", MentionUserIds: ["user-b"])));
        await db.Updateable<ProjectManagementProjectMemberEntity>().SetColumns(member => new ProjectManagementProjectMemberEntity { IsActive = false }).Where(member => member.ProjectId == "project-a" && member.UserId == "user-a").ExecuteCommandAsync();
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("removed", MentionUserIds: ["user-a"])));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("task-a", new ProjectManagementTaskCommentUpsertRequest("cross project", MentionUserIds: ["user-c"])));
    }

    [Fact]
    public void Comment_controller_separates_view_and_manage_permissions()
    {
        Assert.Contains(typeof(ProjectManagementTaskCommentsController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementCommentView);
        Assert.Contains(typeof(ProjectManagementTaskCommentsController).GetMethod(nameof(ProjectManagementTaskCommentsController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementCommentAdd);
    }

    private static FixedAsterErpCurrentUser CreateUser(string userId = "operator") => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
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
