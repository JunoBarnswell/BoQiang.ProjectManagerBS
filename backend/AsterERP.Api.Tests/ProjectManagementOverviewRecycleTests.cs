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
    public async Task Purge_project_requires_configured_high_risk_services_before_graph_cleanup()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "deleted-project", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", IsDeleted = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "deleted-project", TaskCode = "T-1", Title = "task", IsDeleted = true }).ExecuteCommandAsync();

        var service = new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator", PermissionCodes.ProjectManagementProjectPurge));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.PurgeProjectAsync("deleted-project", new ProjectManagementRecyclePurgeRequest(1, "secret", true)));
    }

    [Fact]
    public async Task Purge_preview_allows_project_scoped_history_which_is_removed_at_final_purge()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "deleted-project", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", IsDeleted = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementActivityEntity
        {
            TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "deleted-project", AggregateType = "Project", AggregateId = "deleted-project",
            ActivityType = "deleted", TraceId = "trace", ActorUserId = "operator"
        }).ExecuteCommandAsync();

        var service = new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator", PermissionCodes.ProjectManagementProjectPurge));
        var preview = await service.PreviewPurgeProjectAsync("deleted-project", 1);

        Assert.True(preview.CanExecute);
        Assert.Equal(0, preview.MemberReferenceCount);
        Assert.Equal(0, preview.MilestoneReferenceCount);
        Assert.Equal(0, preview.TaskReferenceCount);
        Assert.Null(preview.BlockingReason);
    }

    [Fact]
    public async Task Purge_preview_rejects_manager_even_when_the_permission_is_granted()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "deleted-project", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "owner", IsDeleted = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "deleted-project", UserId = "manager", RoleCode = "Manager", IsActive = true }).ExecuteCommandAsync();

        var service = new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), CreateUser("manager", PermissionCodes.ProjectManagementProjectPurge));

        var exception = await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.PreviewPurgeProjectAsync("deleted-project", 1));

        Assert.Contains("Owner", exception.Message);
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

    [Fact]
    public async Task Restore_task_keeps_attachment_relation_available_after_task_recovery()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "deleted-task", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "deleted", IsDeleted = true, VersionNo = 2 }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskAttachmentEntity { Id = "attachment", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "deleted-task", FileId = "file-1", FileName = "proof.txt" }).ExecuteCommandAsync();

        await new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator"))
            .RestoreTaskAsync("deleted-task", new ProjectManagementRecycleRestoreRequest(2));

        var attachment = await db.Queryable<ProjectManagementTaskAttachmentEntity>().SingleAsync(item => item.Id == "attachment");
        Assert.False((await db.Queryable<ProjectManagementTaskEntity>().SingleAsync(item => item.Id == "deleted-task")).IsDeleted);
        Assert.False(attachment.IsDeleted);
        Assert.Equal("file-1", attachment.FileId);
    }

    [Fact]
    public async Task Recycle_query_reports_impact_counts_without_exposing_other_workspace_data()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", IsDeleted = true }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "root", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "root", IsDeleted = true },
            new ProjectManagementTaskEntity { Id = "child", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", ParentTaskId = "root", TaskCode = "T-2", Title = "child", IsDeleted = true },
            new ProjectManagementTaskEntity { Id = "other", TenantId = "tenant-b", AppCode = "SYSTEM", ProjectId = "other-project", TaskCode = "T-3", Title = "other", IsDeleted = true }
        }).ExecuteCommandAsync();

        var user = CreateUser("operator");
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementProjectEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), user, "tenant-a", "SYSTEM"));
        var result = await new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), user).QueryAsync(new ProjectManagementRecycleQuery());

        var project = Assert.Single(result.Projects.Items);
        Assert.Equal(2, project.AffectedTaskCount);
        Assert.Equal(1, Assert.Single(result.Tasks.Items, item => item.Id == "root").AffectedDescendantCount);
        Assert.DoesNotContain(result.Tasks.Items, item => item.Id == "other");
    }

    [Fact]
    public async Task Restore_task_rechecks_wip_and_refreshes_dependency_states_inside_the_mutation()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", WipLimit = 1 }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "active", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "active", Status = "InProgress" },
            new ProjectManagementTaskEntity { Id = "deleted", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-2", Title = "deleted", Status = "InProgress", IsDeleted = true }
        }).ExecuteCommandAsync();
        var dependency = new RecordingDependencyService();
        var service = new ProjectManagementRecycleService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator"), dependencyService: dependency);

        var exception = await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RestoreTaskAsync("deleted", new ProjectManagementRecycleRestoreRequest(1)));

        Assert.Contains("WIP", exception.Message);
        Assert.True((await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "deleted").SingleAsync()).IsDeleted);
        Assert.Empty(dependency.RefreshedProjectIds);

        var deleted = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "deleted").SingleAsync();
        deleted.Status = "Todo";
        await db.Updateable(deleted).UpdateColumns(item => new { item.Status }).ExecuteCommandAsync();
        await service.RestoreTaskAsync("deleted", new ProjectManagementRecycleRestoreRequest(1));

        Assert.False((await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "deleted").SingleAsync()).IsDeleted);
        Assert.Equal(["project-a"], dependency.RefreshedProjectIds);
    }

    [Fact]
    public async Task Purge_task_cleans_related_graph_releases_files_and_records_governance_audit()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "predecessor", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "P", Title = "predecessor", IsDeleted = true },
            new ProjectManagementTaskEntity { Id = "successor", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "S", Title = "successor", IsDeleted = true }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskDependencyEntity { Id = "dep", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", PredecessorTaskId = "predecessor", SuccessorTaskId = "successor", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskCommentEntity { Id = "comment", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "predecessor", Markdown = "comment" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskAttachmentEntity { Id = "attachment", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "predecessor", FileId = "file-1", FileName = "proof.txt" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskReminderEntity { Id = "reminder", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "predecessor", RecipientUserId = "operator", ReminderAtUtc = DateTime.UtcNow, TimeZoneId = "UTC", IdempotencyKey = "reminder-1" }).ExecuteCommandAsync();
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser("operator", PermissionCodes.ProjectManagementTaskPurge);
        var files = new RecordingFileStore();
        var pendingDeletes = new RecordingPurgeFileDeletionService();
        var jobs = new RecordingBackgroundJobManager();
        var operations = new RecordingOperationWriter();
        var service = new ProjectManagementRecycleService(accessor, user, riskConfirmation: new AcceptRiskConfirmation(), maintenanceLock: new TestMaintenanceLock(), operationWriter: operations, fileStore: files, purgeFileDeletionService: pendingDeletes, backgroundJobManager: jobs);

        await service.PurgeTaskAsync("predecessor", new ProjectManagementRecycleTaskPurgeRequest(1, "secret", true));

        Assert.Empty(await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "predecessor").ToListAsync());
        Assert.Empty(await db.Queryable<ProjectManagementTaskDependencyEntity>().ToListAsync());
        Assert.Empty(await db.Queryable<ProjectManagementTaskCommentEntity>().Where(item => item.TaskId == "predecessor").ToListAsync());
        Assert.Empty(await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.TaskId == "predecessor").ToListAsync());
        Assert.Empty(await db.Queryable<ProjectManagementTaskReminderEntity>().Where(item => item.TaskId == "predecessor").ToListAsync());
        Assert.Empty(files.DeletedFileIds);
        Assert.Equal(["file-1"], pendingDeletes.ScheduledFileIds);
        Assert.Single(jobs.Args);
        Assert.Single(operations.CompletedImpacts);
    }

    [Fact]
    public async Task Purge_task_database_rollback_keeps_attachment_and_does_not_queue_or_delete_blob()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T", Title = "task", IsDeleted = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskAttachmentEntity { Id = "attachment", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", FileId = "file-1", FileName = "proof.txt" }).ExecuteCommandAsync();
        db.Ado.ExecuteCommand("CREATE TRIGGER fail_purge_task BEFORE DELETE ON pm_tasks BEGIN SELECT RAISE(ABORT, 'forced rollback'); END;");
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser("operator", PermissionCodes.ProjectManagementTaskPurge);
        var files = new RecordingFileStore();
        var service = new ProjectManagementRecycleService(accessor, user, riskConfirmation: new AcceptRiskConfirmation(), maintenanceLock: new TestMaintenanceLock(), fileStore: files, purgeFileDeletionService: new ProjectManagementPurgeFileDeletionService(accessor, user, files), backgroundJobManager: new RecordingBackgroundJobManager());

        await Assert.ThrowsAnyAsync<Exception>(() => service.PurgeTaskAsync("task-a", new ProjectManagementRecycleTaskPurgeRequest(1, "secret", true)));

        Assert.Single(await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.Id == "attachment").ToListAsync());
        Assert.Empty(await db.Queryable<ProjectManagementPurgeFileDeletionEntity>().ToListAsync());
        Assert.Empty(files.DeletedFileIds);
    }

    private static SqlSugarClient CreateDb() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:overview-recycle-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser(string userId, params string[] permissions) => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim("unused", "unused")
    }.Concat(permissions.Select(permission => new Claim(AsterErpClaimTypes.PermissionCode, permission))), "test")));

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

    private sealed class RecordingActivityWriter : IProjectManagementActivityWriter
    {
        public List<ProjectManagementActivityEvent> Events { get; } = [];
        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default) { Events.Add(activity); return Task.CompletedTask; }
    }

    private sealed class AcceptRiskConfirmation : IProjectManagementRiskConfirmationService
    {
        public Task EnsureConfirmedAsync(string currentPassword, bool confirmRisk, CancellationToken cancellationToken = default) => confirmRisk ? Task.CompletedTask : throw new InvalidOperationException();
    }

    private sealed class TestMaintenanceLock : IProjectManagementMaintenanceLock
    {
        public Task<string> AcquireAsync(string lockKey, TimeSpan duration, CancellationToken cancellationToken = default) => Task.FromResult("lock");
        public Task ReleaseAsync(string operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingFileStore : IProjectManagementFileStore
    {
        public List<string> DeletedFileIds { get; } = [];
        public Task DeleteAsync(string fileId, CancellationToken cancellationToken = default) { DeletedFileIds.Add(fileId); return Task.CompletedTask; }
        public Task<Stream> OpenReadAsync(string fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProjectManagementStoredFile> StoreAsync(Microsoft.AspNetCore.Http.IFormFile file, ProjectManagementFileUploadContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingPurgeFileDeletionService : IProjectManagementPurgeFileDeletionService
    {
        public List<string> ScheduledFileIds { get; } = [];
        public Task ScheduleAsync(ISqlSugarClient db, string operationId, IReadOnlyCollection<ProjectManagementTaskAttachmentEntity> attachments, CancellationToken cancellationToken = default)
        {
            ScheduledFileIds.AddRange(attachments.Select(item => item.FileId));
            return Task.CompletedTask;
        }
        public Task ScheduleOrphanAsync(ISqlSugarClient db, string operationId, string fileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryProcessAsync(string operationId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class RecordingBackgroundJobManager : Volo.Abp.BackgroundJobs.IBackgroundJobManager
    {
        public List<ProjectManagementOperationJobArgs> Args { get; } = [];
        public Task<string> EnqueueAsync<TArgs>(TArgs args, Volo.Abp.BackgroundJobs.BackgroundJobPriority priority = Volo.Abp.BackgroundJobs.BackgroundJobPriority.Normal, TimeSpan? delay = null)
        {
            if (args is ProjectManagementOperationJobArgs operationArgs) Args.Add(operationArgs);
            return Task.FromResult(Guid.NewGuid().ToString("N"));
        }
    }

    private sealed class RecordingOperationWriter : IProjectManagementOperationWriter
    {
        public List<string> CompletedImpacts { get; } = [];
        public Task StartAsync(string operationId, string operationType, string impactJson, string traceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CompleteWithImpactAsync(string operationId, string impactJson, CancellationToken cancellationToken = default) { CompletedImpacts.Add(impactJson); return Task.CompletedTask; }
        public Task CreatePendingAsync(string operationId, string operationType, string impactJson, string traceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ReportProgressAsync(string operationId, string phase, int progressPercent, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> IsCancellationRequestedAsync(string operationId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RequestCancellationAsync(string operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CancelAsync(string operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SucceedAsync(string operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task FailAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task FailRunningExceptAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingDependencyService : IProjectManagementTaskDependencyService
    {
        public List<string> RefreshedProjectIds { get; } = [];
        public Task<IReadOnlyList<ProjectManagementTaskDependencyResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProjectManagementTaskDependencyResponse> CreateAsync(string projectId, ProjectManagementTaskDependencyUpsertRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProjectManagementTaskDependencyResponse>> CreateBatchAsync(string projectId, ProjectManagementTaskDependencyBatchCreateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProjectManagementTaskDependencyForceStartResponse> ForceStartAsync(string projectId, string taskId, ProjectManagementTaskDependencyForceStartRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureCanStartAsync(string projectId, string taskId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> PurgeForTasksAsync(string projectId, IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> PurgeDeletedTasksAsync(string projectId, IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RefreshBlockedStatesAsync(string projectId, CancellationToken cancellationToken = default)
        {
            RefreshedProjectIds.Add(projectId);
            return Task.CompletedTask;
        }
    }
}
