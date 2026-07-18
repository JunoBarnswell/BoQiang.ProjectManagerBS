using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskBatchServiceTests
{
    [Fact]
    public async Task Batch_update_checks_wip_before_mutating_task_statuses()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-batch-wip-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", WipLimit = 1 }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "A", Title = "A", CreatedBy = "operator", CreatedTime = DateTime.UtcNow },
            new ProjectManagementTaskEntity { Id = "task-b", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "B", Title = "B", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskBatchService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync(new ProjectManagementTaskBatchUpdateRequest("project-a", [new("task-a", 1), new("task-b", 1)], Status: "InProgress")));

        Assert.All(await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == "project-a").ToListAsync(), item => Assert.Equal("Todo", item.Status));
    }

    [Fact]
    public async Task Batch_update_is_atomic_and_rejects_stale_versions()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-batch-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "A", Title = "A", CreatedBy = "operator", CreatedTime = DateTime.UtcNow },
            new ProjectManagementTaskEntity { Id = "task-b", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "B", Title = "B", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskBatchService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var request = new ProjectManagementTaskBatchUpdateRequest("project-a", [new("task-a", 1), new("task-b", 1)], Priority: "High");
        var result = await service.UpdateAsync(request);
        Assert.All(result, item => Assert.Equal("High", item.Priority));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync(request with { Status = "Done" }));
        var rows = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == "project-a").ToListAsync();
        Assert.All(rows, item => Assert.Equal("Todo", item.Status));
    }

    [Fact]
    public async Task Batch_update_commits_activity_and_sync_before_publishing_realtime_invalidations()
    {
        using var db = CreateDatabase("side-effects");
        await SeedTasksAsync(db);
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser();
        var publisher = new CommitAwareRealtimePublisher(db);
        var service = new ProjectManagementTaskBatchService(
            accessor,
            user,
            activityWriter: new ProjectManagementActivityService(accessor, user),
            syncJournalWriter: new ProjectManagementSyncJournalWriter(accessor),
            realtimePublisher: publisher);

        await service.UpdateAsync(new ProjectManagementTaskBatchUpdateRequest(
            "project-a", [new("task-a", 1), new("task-b", 1)], Priority: "High"));

        Assert.Equal(1, await db.Queryable<ProjectManagementActivityEntity>().CountAsync());
        Assert.Equal(2, await db.Queryable<ProjectManagementSyncJournalEntity>().CountAsync());
        var timeline = await new ProjectManagementActivityService(accessor, user)
            .QueryAsync("project-a", new ProjectManagementActivityQuery());
        var activity = Assert.Single(timeline.Items);
        Assert.Equal("TaskBatch", activity.AggregateType);
        Assert.Equal(2, activity.Batch!.TotalCount);
        Assert.Equal(2, activity.Batch.Details!.Count);
        Assert.Equal(2, publisher.ObservedCommittedSideEffects.Count);
        Assert.All(publisher.ObservedCommittedSideEffects, item => Assert.Equal((1, 2), item));
    }

    [Fact]
    public async Task Batch_update_rolls_back_tasks_activity_and_sync_and_does_not_publish_when_projection_fails()
    {
        using var db = CreateDatabase("projection-failure");
        await SeedTasksAsync(db);
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser();
        var publisher = new CommitAwareRealtimePublisher(db);
        var service = new ProjectManagementTaskBatchService(
            accessor,
            user,
            progressProjector: new FailingProgressProjector(),
            activityWriter: new ProjectManagementActivityService(accessor, user),
            syncJournalWriter: new ProjectManagementSyncJournalWriter(accessor),
            realtimePublisher: publisher);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(
            new ProjectManagementTaskBatchUpdateRequest("project-a", [new("task-a", 1), new("task-b", 1)], Priority: "High")));

        Assert.All(await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == "project-a").ToListAsync(), item => Assert.Equal("Medium", item.Priority));
        Assert.Equal(0, await db.Queryable<ProjectManagementActivityEntity>().CountAsync());
        Assert.Equal(0, await db.Queryable<ProjectManagementSyncJournalEntity>().CountAsync());
        Assert.Empty(publisher.ObservedCommittedSideEffects);
    }

    [Fact]
    public async Task Batch_update_applies_schedule_milestone_and_labels_as_one_command()
    {
        using var db = CreateDatabase("fields-and-labels");
        await SeedTasksAsync(db);
        await db.Insertable(new ProjectManagementMilestoneEntity
        {
            Id = "milestone-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", MilestoneName = "Release", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementLabelEntity
        {
            Id = "label-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", LabelName = "Urgent", Color = "#EF4444", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskBatchService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var startDate = new DateTime(2026, 7, 20);
        var dueDate = new DateTime(2026, 7, 31);

        var result = await service.UpdateAsync(new ProjectManagementTaskBatchUpdateRequest(
            "project-a",
            [new("task-a", 1), new("task-b", 1)],
            Priority: "High",
            AssigneeUserId: "assignee-a",
            MilestoneId: "milestone-a",
            UpdateMilestone: true,
            StartDate: startDate,
            DueDate: dueDate,
            UpdateSchedule: true,
            LabelIds: ["label-a"],
            UpdateLabels: true));

        Assert.Equal(2, result.Count);
        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == "project-a").ToListAsync();
        Assert.All(tasks, task =>
        {
            Assert.Equal("High", task.Priority);
            Assert.Equal("assignee-a", task.AssigneeUserId);
            Assert.Equal("milestone-a", task.MilestoneId);
            Assert.Equal(startDate, task.StartDate);
            Assert.Equal(dueDate, task.DueDate);
            Assert.Equal(2, task.VersionNo);
        });
        Assert.Equal(2, await db.Queryable<ProjectManagementTaskLabelEntity>().Where(item => item.ProjectId == "project-a" && item.LabelId == "label-a" && !item.IsDeleted).CountAsync());
    }

    [Fact]
    public async Task Batch_update_rejects_wip_override_without_the_dedicated_permission()
    {
        using var db = CreateDatabase("wip-override-permission");
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", WipLimit = 0
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity
        {
            Id = "task-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "A", Title = "A", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskBatchService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync(
            new ProjectManagementTaskBatchUpdateRequest("project-a", [new("task-a", 1)], Status: "InProgress", OverrideWip: true)));

        Assert.Equal("Todo", (await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "task-a").FirstAsync()).Status);
    }

    private static SqlSugarClient CreateDatabase(string suffix)
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-batch-{suffix}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        return db;
    }

    private static async Task SeedTasksAsync(ISqlSugarClient db)
    {
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "A", Title = "A", CreatedBy = "operator", CreatedTime = DateTime.UtcNow },
            new ProjectManagementTaskEntity { Id = "task-b", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "B", Title = "B", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }
        }).ExecuteCommandAsync();
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

    private sealed class FailingProgressProjector : IProjectManagementTaskProgressProjector
    {
        public Task RefreshAsync(string projectId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("projection failed");
    }

    private sealed class CommitAwareRealtimePublisher(ISqlSugarClient db) : IProjectManagementRealtimePublisher
    {
        public List<(int ActivityCount, int SyncCount)> ObservedCommittedSideEffects { get; } = [];

        public async Task PublishInvalidationAsync(ProjectManagementDataInvalidationEvent invalidation, CancellationToken cancellationToken = default)
        {
            var activityCount = await db.Queryable<ProjectManagementActivityEntity>().CountAsync(cancellationToken);
            var syncCount = await db.Queryable<ProjectManagementSyncJournalEntity>().CountAsync(cancellationToken);
            ObservedCommittedSideEffects.Add((activityCount, syncCount));
        }
    }
}
