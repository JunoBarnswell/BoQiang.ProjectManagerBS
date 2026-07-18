using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskParticipantServiceTests
{
    [Fact]
    public async Task Participant_assignment_requires_an_active_member_and_keeps_history_when_member_is_disabled()
    {
        using var db = CreateDb("member-guard-history");
        await SeedProjectAndTaskAsync(db);
        await db.Insertable(new[]
        {
            Member("member-active", "active-user", true),
            Member("member-disabled", "disabled-user", false)
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskParticipantEntity
        {
            Id = "historic-participant", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", UserId = "disabled-user", RoleCode = "Participant", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskParticipantService(
            new TestWorkspaceDatabaseAccessor(db), CreateUser(), new CandidateService("active-user"));

        var history = await service.QueryAsync("task-a");
        var retained = Assert.Single(history);
        Assert.False(retained.IsProjectMemberActive);
        Assert.True(retained.IsCurrentAssignment);

        var rejected = await Assert.ThrowsAsync<ValidationException>(() => service.AddAsync(
            "task-a", new ProjectManagementTaskParticipantUpsertRequest("disabled-user", VersionNo: 1)));
        Assert.Equal("参与人必须来自当前租户和应用的启用用户/任职", rejected.Message);

        var added = await service.AddAsync("task-a", new ProjectManagementTaskParticipantUpsertRequest("active-user", VersionNo: 1));
        Assert.Equal("active-user", added.UserId);
        Assert.True(added.IsProjectMemberActive);

        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "B", ProjectName = "B", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-b", TaskCode = "B", Title = "B", VersionNo = 1, CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await Assert.ThrowsAsync<ValidationException>(() => service.AddAsync("task-b", new ProjectManagementTaskParticipantUpsertRequest("active-user", VersionNo: 1)));
    }

    [Fact]
    public async Task Participant_changes_write_activity_then_notify_after_commit_and_history_retains_removed_relation()
    {
        using var db = CreateDb("activity-notification-history");
        await SeedProjectAndTaskAsync(db);
        await db.Insertable(Member("member-active", "active-user", true)).ExecuteCommandAsync();
        var activityWriter = new CapturingActivityWriter();
        var notificationPublisher = new CapturingNotificationPublisher();
        var service = new ProjectManagementTaskParticipantService(
            new TestWorkspaceDatabaseAccessor(db), CreateUser(), new CandidateService("active-user"),
            activityWriter: activityWriter, notificationPublisher: notificationPublisher);

        var added = await service.AddAsync("task-a", new ProjectManagementTaskParticipantUpsertRequest("active-user", VersionNo: 1));
        Assert.Equal("task.participant.added", Assert.Single(activityWriter.Events).ActivityType);
        Assert.Equal("task.participant.added", Assert.Single(notificationPublisher.Notifications).NotificationType);

        await service.RemoveAsync("task-a", added.Id, 2);
        Assert.Equal(2, activityWriter.Events.Count);
        Assert.Equal("task.participant.removed", activityWriter.Events[1].ActivityType);
        Assert.Equal(2, notificationPublisher.Notifications.Count);
        Assert.Equal("task.participant.removed", notificationPublisher.Notifications[1].NotificationType);

        Assert.Empty(await service.QueryAsync("task-a"));
        var historical = Assert.Single(await service.QueryHistoryAsync("task-a"));
        Assert.False(historical.IsCurrentAssignment);
        Assert.True(historical.IsProjectMemberActive);
    }

    [Fact]
    public async Task Candidate_projection_is_project_scoped_and_bulk_projection_returns_multiple_tasks_without_per_task_loading()
    {
        using var db = CreateDb("candidate-projection");
        db.CodeFirst.InitTables<SystemUserEntity, SystemUserTenantMembershipEntity>();
        await SeedProjectAndTaskAsync(db);
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "B", Title = "B", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            Member("member-active", "active-user", true),
            Member("member-disabled", "disabled-user", false),
            new ProjectManagementProjectMemberEntity { Id = "other-project-member", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-b", UserId = "other-user", RoleCode = "Member", IsActive = true, CreatedBy = "operator", CreatedTime = DateTime.UtcNow, JoinedAt = DateTime.UtcNow }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new SystemUserEntity { Id = "active-user", UserName = "active", DisplayName = "Active", Status = "Enabled" },
            new SystemUserEntity { Id = "disabled-user", UserName = "disabled", DisplayName = "Disabled", Status = "Disabled" },
            new SystemUserEntity { Id = "other-user", UserName = "other", DisplayName = "Other", Status = "Enabled" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new SystemUserTenantMembershipEntity { Id = "membership-active", UserId = "active-user", TenantId = "tenant-a", Status = "Enabled" },
            new SystemUserTenantMembershipEntity { Id = "membership-disabled", UserId = "disabled-user", TenantId = "tenant-a", Status = "Enabled" },
            new SystemUserTenantMembershipEntity { Id = "membership-other", UserId = "other-user", TenantId = "tenant-a", Status = "Enabled" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskParticipantEntity { Id = "participant-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", UserId = "active-user", CreatedBy = "operator", CreatedTime = DateTime.UtcNow },
            new ProjectManagementTaskParticipantEntity { Id = "participant-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-b", UserId = "disabled-user", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }
        }).ExecuteCommandAsync();
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var currentUser = CreateUser();
        var projection = new ProjectManagementTaskParticipantProjection(accessor, currentUser);
        var service = new ProjectManagementTaskParticipantService(accessor, currentUser, new CandidateService("active-user"), projection: projection);

        var candidates = await service.QueryCandidatesAsync("task-a", new ProjectManagementTaskParticipantCandidateQuery());
        Assert.Equal("active-user", Assert.Single(candidates.Items).UserId);
        var byTask = await projection.LoadByTaskIdsAsync(["task-a", "task-b"], includeHistorical: false);
        Assert.Equal(2, byTask.Count);
        Assert.True(byTask["task-a"].Single().IsProjectMemberActive);
        Assert.False(byTask["task-b"].Single().IsProjectMemberActive);

        await db.Insertable(new ProjectManagementTaskEntity { Id = "foreign-task", TenantId = "tenant-b", AppCode = "SYSTEM", ProjectId = "foreign-project", TaskCode = "F", Title = "F", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => service.QueryAsync("foreign-task"));
    }

    [Fact]
    public async Task Batch_participant_replacement_stays_in_the_caller_transaction_and_publishes_only_after_commit()
    {
        using var db = CreateDb("batch-transaction");
        await SeedProjectAndTaskAsync(db);
        db.CodeFirst.InitTables<SystemUserEntity, SystemUserTenantMembershipEntity>();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "B", Title = "B", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        var scopedMember = Member("member-b", "user-b", true);
        scopedMember.ScopeRootTaskId = "task-b";
        await db.Insertable(new[] { Member("member-a", "user-a", true), scopedMember }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new SystemUserEntity { Id = "user-a", UserName = "a", DisplayName = "User A", Status = "Enabled" },
            new SystemUserEntity { Id = "user-b", UserName = "b", DisplayName = "User B", Status = "Enabled" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new SystemUserTenantMembershipEntity { Id = "tenant-a-user-a", UserId = "user-a", TenantId = "tenant-a", Status = "Enabled" },
            new SystemUserTenantMembershipEntity { Id = "tenant-a-user-b", UserId = "user-b", TenantId = "tenant-a", Status = "Enabled" }
        }).ExecuteCommandAsync();
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var currentUser = CreateUser();
        var notifications = new CapturingNotificationPublisher();
        var service = new ProjectManagementTaskParticipantService(
            accessor, currentUser, new CandidateService("user-a", "user-b"),
            activityWriter: new ProjectManagementActivityService(accessor, currentUser), notificationPublisher: notifications);
        var request = new ProjectManagementTaskParticipantBatchReplaceRequest("project-a", [
            new ProjectManagementTaskParticipantBatchReplaceItem("task-a", ["user-a"]),
            new ProjectManagementTaskParticipantBatchReplaceItem("task-b", ["user-b"])], "batch-trace");

        db.Ado.BeginTran();
        await Assert.ThrowsAsync<ValidationException>(() => service.ReplaceParticipantsForTasksAsync(db,
            new ProjectManagementTaskParticipantBatchReplaceRequest("project-a", [new ProjectManagementTaskParticipantBatchReplaceItem("task-a", ["user-b"])])));
        db.Ado.RollbackTran();

        db.Ado.BeginTran();
        await service.ReplaceParticipantsForTasksAsync(db, request);
        db.Ado.RollbackTran();
        Assert.Equal(0, await db.Queryable<ProjectManagementTaskParticipantEntity>().Where(item => !item.IsDeleted).CountAsync());
        Assert.Equal(0, await db.Queryable<ProjectManagementActivityEntity>().CountAsync());
        Assert.Empty(notifications.Notifications);

        db.Ado.BeginTran();
        var committed = await service.ReplaceParticipantsForTasksAsync(db, request);
        Assert.Equal(2, committed.Tasks.Count);
        Assert.All(committed.Tasks, item => Assert.Single(item.AddedUserIds));
        Assert.Empty(notifications.Notifications);
        db.Ado.CommitTran();
        await service.PublishCommittedBatchMutationAsync(committed);
        Assert.Equal(2, await db.Queryable<ProjectManagementTaskParticipantEntity>().Where(item => !item.IsDeleted).CountAsync());
        Assert.Equal(2, await db.Queryable<ProjectManagementActivityEntity>().CountAsync());
        Assert.Equal(2, notifications.Notifications.Count);

        db.Ado.BeginTran();
        var removed = await service.ReplaceParticipantsForTasksAsync(db, new ProjectManagementTaskParticipantBatchReplaceRequest("project-a", [
            new ProjectManagementTaskParticipantBatchReplaceItem("task-a", []),
            new ProjectManagementTaskParticipantBatchReplaceItem("task-b", ["user-b"])], "batch-trace-remove"));
        db.Ado.CommitTran();
        await service.PublishCommittedBatchMutationAsync(removed);
        var taskAResult = Assert.Single(removed.Tasks, item => item.TaskId == "task-a");
        Assert.Equal("user-a", Assert.Single(taskAResult.RemovedUserIds));
        Assert.Single(await service.QueryHistoryAsync("task-a"));
        Assert.Equal(3, notifications.Notifications.Count);
    }

    private static async Task SeedProjectAndTaskAsync(ISqlSugarClient db)
    {
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "A", Title = "A", VersionNo = 1, CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
    }

    private static ProjectManagementProjectMemberEntity Member(string id, string userId, bool isActive) => new()
    {
        Id = id, TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = userId, RoleCode = "Member", IsActive = isActive,
        CreatedBy = "operator", CreatedTime = DateTime.UtcNow, JoinedAt = DateTime.UtcNow
    };

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
    }, "test")));

    private static SqlSugarClient CreateDb(string suffix) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-participants-{suffix}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class CandidateService(params string[] selectableUserIds) : IProjectManagementMemberCandidateService
    {
        private readonly HashSet<string> selectable = selectableUserIds.ToHashSet(StringComparer.Ordinal);
        public Task<bool> IsSelectableAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(selectable.Contains(userId));
        public Task<GridPageResult<ProjectManagementMemberCandidateResponse>> QueryAsync(ProjectManagementMemberCandidateQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CapturingActivityWriter : IProjectManagementActivityWriter
    {
        public List<ProjectManagementActivityEvent> Events { get; } = [];
        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default) { Events.Add(activity); return Task.CompletedTask; }
    }

    private sealed class CapturingNotificationPublisher : IProjectManagementNotificationPublisher
    {
        public List<ProjectManagementNotification> Notifications { get; } = [];
        public Task PublishAsync(ProjectManagementNotification notification, CancellationToken cancellationToken = default) { Notifications.Add(notification); return Task.CompletedTask; }
    }
}
