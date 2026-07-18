using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementActivityServiceTests
{
    [Fact]
    public async Task Append_persists_field_changes_batch_details_and_masks_sensitive_values()
    {
        using var db = await CreateDbAsync("activity-payload");
        var service = new ProjectManagementActivityService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        await service.AppendAsync(new ProjectManagementActivityEvent(
            "tenant-a", "SYSTEM", "Task", "task-a", "task.updated", "更新任务", "trace-activity", "operator", "project-a",
            FieldChanges:
            [
                new ProjectManagementActivityFieldChange("Status", "状态", "Todo", "Done"),
                new ProjectManagementActivityFieldChange("ApiToken", "令牌", "old-secret", "new-secret")
            ],
            Batch: new ProjectManagementActivityBatch("batch-1", 2, 2, 0,
            [
                new ProjectManagementActivityBatchItem("Task", "task-a", "更新任务 A"),
                new ProjectManagementActivityBatchItem("Task", "task-b", "更新任务 B")
            ])));

        var stored = await db.Queryable<ProjectManagementActivityEntity>().SingleAsync(item => item.AggregateId == "task-a");
        Assert.DoesNotContain("old-secret", stored.Remark);
        Assert.DoesNotContain("new-secret", stored.Remark);

        var page = await service.QueryAsync("project-a", new ProjectManagementActivityQuery());
        var activity = Assert.Single(page.Items);
        Assert.Equal("trace-activity", activity.TraceId);
        Assert.Equal("Todo", activity.FieldChanges![0].Before);
        Assert.Equal("[已脱敏]", activity.FieldChanges![1].Before);
        Assert.True(activity.FieldChanges![1].IsSensitive);
        Assert.Equal("batch-1", activity.Batch!.OperationId);
        Assert.Equal(2, activity.Batch.Details!.Count);
    }

    [Fact]
    public async Task Timeline_is_stably_paginated_and_keeps_soft_deleted_activity_evidence()
    {
        using var db = await CreateDbAsync("activity-page");
        var timestamp = new DateTime(2026, 7, 19, 8, 0, 0, DateTimeKind.Utc);
        await db.Insertable(new[]
        {
            Activity("activity-a", "task-a", timestamp, isDeleted: true),
            Activity("activity-b", "task-b", timestamp),
            Activity("activity-c", "task-c", timestamp.AddMinutes(-1))
        }).ExecuteCommandAsync();
        var service = new ProjectManagementActivityService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        var first = await service.QueryAsync("project-a", new ProjectManagementActivityQuery(PageIndex: 1, PageSize: 2));
        var second = await service.QueryAsync("project-a", new ProjectManagementActivityQuery(PageIndex: 2, PageSize: 2));

        Assert.Equal(3, first.Total);
        Assert.Equal(["activity-b", "activity-a"], first.Items.Select(item => item.Id));
        Assert.Equal(["activity-c"], second.Items.Select(item => item.Id));
        Assert.Contains(first.Items, item => item.Id == "activity-a");
    }

    [Fact]
    public async Task Timeline_rejects_invalid_time_range_and_oversized_batch_details()
    {
        using var db = await CreateDbAsync("activity-validation");
        var service = new ProjectManagementActivityService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.QueryAsync(
            "project-a", new ProjectManagementActivityQuery(From: DateTime.UtcNow, To: DateTime.UtcNow.AddMinutes(-1))));

        var details = Enumerable.Range(0, 201)
            .Select(index => new ProjectManagementActivityBatchItem("Task", $"task-{index}", null))
            .ToList();
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.AppendAsync(new ProjectManagementActivityEvent(
            "tenant-a", "SYSTEM", "Task", "task-a", "batch.updated", "批量更新", "trace-batch", "operator", "project-a",
            Batch: new ProjectManagementActivityBatch("batch-too-many", 201, 201, 0, details))));
    }

    [Fact]
    public async Task Timeline_uses_registered_orm_filter_for_project_membership()
    {
        using var db = await CreateDbAsync("activity-filter");
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "project-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "B", ProjectName = "B", OwnerUserId = "other-user" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            Activity("activity-visible", "task-a", DateTime.UtcNow, projectId: "project-a"),
            Activity("activity-hidden", "task-b", DateTime.UtcNow, projectId: "project-b")
        }).ExecuteCommandAsync();

        var user = CreateUser();
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementActivityEntity), user, "tenant-a", "SYSTEM"));
        var service = new ProjectManagementActivityService(new TestWorkspaceDatabaseAccessor(db), user);

        Assert.Single((await service.QueryAsync("project-a", new ProjectManagementActivityQuery())).Items);
        Assert.Empty((await service.QueryAsync("project-b", new ProjectManagementActivityQuery())).Items);
    }

    private static ProjectManagementActivityEntity Activity(string id, string aggregateId, DateTime createdTime, bool isDeleted = false, string projectId = "project-a") => new()
    {
        Id = id,
        TenantId = "tenant-a",
        AppCode = "SYSTEM",
        ProjectId = projectId,
        AggregateType = "Task",
        AggregateId = aggregateId,
        ActivityType = "task.updated",
        Summary = "更新任务",
        TraceId = $"trace-{id}",
        ActorUserId = "operator",
        CreatedBy = "operator",
        CreatedTime = createdTime,
        IsDeleted = isDeleted
    };

    private static async Task<SqlSugarClient> CreateDbAsync(string name)
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        return db;
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim(AsterErpClaimTypes.DataScope, "SELF")
    }, "test")));
}
