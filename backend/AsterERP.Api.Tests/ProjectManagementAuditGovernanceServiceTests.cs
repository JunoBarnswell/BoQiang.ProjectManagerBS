using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementAuditGovernanceServiceTests
{
    [Fact]
    public async Task Cleanup_archives_normal_activity_deletes_expired_archive_and_preserves_high_risk_activity()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-audit-governance-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var now = DateTime.UtcNow;
        await db.Insertable(new[]
        {
            new ProjectManagementActivityEntity { Id = "normal-archive", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", AggregateType = "Task", AggregateId = "task-a", ActivityType = "task.updated", Summary = "普通活动", TraceId = "trace-a", ActorUserId = "operator", CreatedBy = "operator", CreatedTime = now.AddDays(-200) },
            new ProjectManagementActivityEntity { Id = "normal-delete", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", AggregateType = "Task", AggregateId = "task-b", ActivityType = "task.updated", Summary = "历史普通活动", TraceId = "trace-b", ActorUserId = "operator", CreatedBy = "operator", CreatedTime = now.AddDays(-3000), ArchivedTime = now.AddDays(-2600) },
            new ProjectManagementActivityEntity { Id = "high-risk", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", AggregateType = "Task", AggregateId = "task-c", ActivityType = "backup.restore", Summary = "恢复备份", TraceId = "trace-c", ActorUserId = "operator", CreatedBy = "operator", CreatedTime = now.AddDays(-3000) }
        }).ExecuteCommandAsync();

        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser();
        var service = new ProjectManagementAuditGovernanceService(accessor, user, new ProjectManagementOperationWriter(accessor, user), new RecordingBackgroundJobManager());
        var started = await service.StartCleanupAsync();
        await service.ExecuteCleanupAsync(started.Id);

        var rows = await db.Queryable<ProjectManagementActivityEntity>().Where(item => item.TenantId == "tenant-a").ToListAsync();
        var archived = Assert.Single(rows, item => item.Id == "normal-archive");
        var deleted = Assert.Single(rows, item => item.Id == "normal-delete");
        var highRisk = Assert.Single(rows, item => item.Id == "high-risk");
        Assert.NotNull(archived.ArchivedTime);
        Assert.False(archived.IsDeleted);
        Assert.True(deleted.IsDeleted);
        Assert.False(highRisk.IsDeleted);
        var operation = await db.Queryable<ProjectManagementOperationEntity>().SingleAsync(item => item.Id == started.Id);
        Assert.Equal("Succeeded", operation.Status);
        Assert.Contains("highRiskPreservedCount", operation.ImpactJson, StringComparison.Ordinal);
        Assert.Contains("deletedCount", operation.ImpactJson, StringComparison.Ordinal);
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim(AsterErpClaimTypes.DataScope, "SELF"),
        new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementOperationManage)
    }, "test")));

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient GetProjectManagementDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> GetProjectManagementDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class RecordingBackgroundJobManager : IBackgroundJobManager
    {
        public Task<string> EnqueueAsync<TArgs>(TArgs args, BackgroundJobPriority priority = BackgroundJobPriority.Normal, TimeSpan? delay = null) => Task.FromResult(Guid.NewGuid().ToString("N"));
    }
}
