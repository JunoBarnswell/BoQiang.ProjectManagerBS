using System.Security.Claims;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Volo.Abp.BackgroundJobs;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementAuditServiceTests
{
    [Fact]
    public async Task Audit_query_and_export_use_project_scope_and_csv_escaping()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-audit-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementActivityEntity
        {
            Id = "activity-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", AggregateType = "Task", AggregateId = "task-a",
            ActivityType = "updated", Summary = "标题, \"已更新\"", TraceId = "trace-a", ActorUserId = "operator", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();

        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = new FixedAsterErpCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AsterErpClaimTypes.UserId, "operator"),
            new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
            new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
        }, "test")));
        var service = new ProjectManagementAuditService(accessor, user, new ProjectManagementAccessPolicy(accessor, user));

        var page = await service.QueryAsync(new ProjectManagementAuditQuery(ProjectId: "project-a", Keyword: "已更新"));
        Assert.Single(page.Items);
        var export = await service.ExportAsync(new ProjectManagementAuditQuery(ProjectId: "project-a"));
        var csv = System.Text.Encoding.UTF8.GetString(export.Content);
        Assert.Contains("\"标题, \"\"已更新\"\"\"", csv, StringComparison.Ordinal);
        Assert.Equal(1, export.Count);
    }

    [Fact]
    public async Task Audit_query_applies_combined_filters_sorting_and_device_lookup_in_database()
    {
        using var db = CreateDatabase();
        await SeedAuditDataAsync(db);
        var user = CreateUser("manager");
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementActivityEntity), user, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementSyncJournalEntity), user, "tenant-a", "SYSTEM"));
        var service = new ProjectManagementAuditService(new TestWorkspaceDatabaseAccessor(db), user, new ProjectManagementAccessPolicy(new TestWorkspaceDatabaseAccessor(db), user));

        var page = await service.QueryAsync(new ProjectManagementAuditQuery(
            ProjectId: "project-visible",
            AggregateType: "Task",
            ActivityType: "task.updated",
            ActorUserId: "manager",
            ActorRole: "Manager",
            Source: "User",
            SourceDeviceId: "device-a",
            IsSuccess: true,
            Keyword: "planning",
            From: DateTime.UtcNow.AddDays(-1),
            To: DateTime.UtcNow.AddDays(1),
            Sorts: [new GridSort { Field = "actorUserId", Order = "asc" }]));

        var item = Assert.Single(page.Items);
        Assert.Equal("visible-success", item.Id);
        Assert.Equal("User", item.Source);
        Assert.Equal("device-a", item.SourceDeviceId);
        Assert.True(item.IsSuccess);

        await Assert.ThrowsAsync<ValidationException>(() => service.QueryAsync(new ProjectManagementAuditQuery(
            From: DateTime.UtcNow.AddDays(-1), To: DateTime.UtcNow.AddDays(1), Sorts: [new GridSort { Field = "summary", Order = "asc" }])));
        await Assert.ThrowsAsync<ValidationException>(() => service.QueryAsync(new ProjectManagementAuditQuery(
            From: DateTime.UtcNow.AddDays(-93), To: DateTime.UtcNow)));
    }

    [Fact]
    public async Task Audit_detail_redacts_legacy_sensitive_values_and_returns_trace_context_for_deleted_target()
    {
        using var db = CreateDatabase();
        var now = DateTime.UtcNow;
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-detail", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "DETAIL", ProjectName = "Detail", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            source = "Import",
            fieldChanges = new[]
            {
                new { field = "password", displayName = "口令", before = "legacy-secret", after = "new-secret", isSensitive = false },
                new { field = "config", displayName = "配置", before = "{\"enabled\":false}", after = "{\"enabled\":true}", isSensitive = false }
            },
            batch = new { operationId = "import-1", totalCount = 1, successCount = 1, failureCount = 0, details = Array.Empty<object>() }
        });
        await db.Insertable(new ProjectManagementActivityEntity
        {
            Id = "detail-activity", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-detail", AggregateType = "Task", AggregateId = "deleted-task",
            ActivityType = "import.completed", Summary = "导入完成", TraceId = "trace-detail", ActorUserId = "operator", CreatedBy = "operator", CreatedTime = now, Remark = payload
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementOperationEntity
        {
            Id = "operation-detail", TenantId = "tenant-a", AppCode = "SYSTEM", OperationType = "import.excel", Status = "Succeeded", Phase = "Completed", ImpactJson = "{}", TraceId = "trace-detail", ActorUserId = "operator", StartedTime = now.AddSeconds(1), CreatedBy = "operator", CreatedTime = now.AddSeconds(1)
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementSyncJournalEntity
        {
            Id = "journal-detail", TenantId = "tenant-a", AppCode = "SYSTEM", SequenceNo = 1, ProjectId = "project-detail", AggregateType = "Task", AggregateId = "deleted-task", Operation = "updated", VersionNo = 1, PayloadJson = "{}", ActorUserId = "operator", TraceId = "trace-detail", CreatedBy = "operator", CreatedTime = now.AddSeconds(2)
        }).ExecuteCommandAsync();
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser("operator");
        var service = new ProjectManagementAuditService(accessor, user, new ProjectManagementAccessPolicy(accessor, user));

        var detail = await service.GetDetailAsync("detail-activity");

        Assert.True(detail.EntitySnapshot.IsDeleted);
        Assert.Equal("[已脱敏]", detail.FieldChanges.Single(item => item.Field == "password").Before);
        Assert.Equal("[已脱敏]", detail.FieldChanges.Single(item => item.Field == "password").After);
        Assert.Contains(detail.RelatedEvents, item => item.Kind == "Operation" && item.Causality == "Followed");
        Assert.Contains(detail.RelatedEvents, item => item.Kind == "SyncJournal" && item.Causality == "Followed");
        Assert.Contains(detail.References, item => item.Kind == "BatchOperation" && item.Id == "import-1");
        Assert.Contains(detail.References, item => item.Kind == "Import" && item.Id == "deleted-task");
        Assert.Null(detail.TraceDiagnosticsRoute);
    }

    [Fact]
    public async Task Audit_query_never_returns_activities_after_project_membership_is_revoked()
    {
        using var db = CreateDatabase();
        await SeedAuditDataAsync(db);
        var revokedUser = CreateUser("revoked");
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementActivityEntity), revokedUser, "tenant-a", "SYSTEM"));
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var service = new ProjectManagementAuditService(accessor, revokedUser, new ProjectManagementAccessPolicy(accessor, revokedUser));

        var page = await service.QueryAsync(new ProjectManagementAuditQuery(From: DateTime.UtcNow.AddDays(-1), To: DateTime.UtcNow.AddDays(1)));

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Audit_export_runs_as_background_operation_and_redacts_sensitive_values()
    {
        using var db = CreateDatabase();
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-export", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "EXPORT", ProjectName = "Export", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementActivityEntity
        {
            Id = "export-activity", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-export", AggregateType = "Task", AggregateId = "task-export",
            ActivityType = "task.updated", Summary = "=unsafe", TraceId = "trace-export", ActorUserId = "operator", CreatedBy = "operator", CreatedTime = DateTime.UtcNow,
            Remark = "{\"fieldChanges\":[{\"field\":\"password\",\"displayName\":\"口令\",\"before\":\"old-secret\",\"after\":\"new-secret\",\"isSensitive\":false}]}"
        }).ExecuteCommandAsync();

        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser("operator");
        var queue = new RecordingBackgroundJobManager();
        var service = new ProjectManagementAuditService(
            accessor,
            user,
            new ProjectManagementAccessPolicy(accessor, user),
            new ProjectManagementOperationWriter(accessor, user),
            queue,
            new TestHostEnvironment(Path.Combine(Path.GetTempPath(), $"pm-audit-{Guid.NewGuid():N}")));

        await Assert.ThrowsAsync<ValidationException>(() => service.StartExportAsync(new ProjectManagementAuditExportRequest(
            new ProjectManagementAuditQuery(ProjectId: "project-export"),
            ["FieldChanges"],
            IncludeSensitive: true)));

        var started = await service.StartExportAsync(new ProjectManagementAuditExportRequest(
            new ProjectManagementAuditQuery(ProjectId: "project-export"),
            ["Summary", "FieldChanges"]));

        Assert.Equal(started.OperationId, Assert.Single(queue.Args).OperationId);
        await service.ExecuteExportAsync(started.OperationId);
        var exported = await service.DownloadExportAsync(started.OperationId);
        var csv = System.Text.Encoding.UTF8.GetString(exported.Content);

        Assert.Contains("\"'=unsafe\"", csv, StringComparison.Ordinal);
        Assert.Contains("password", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("old-secret", csv, StringComparison.Ordinal);
        var operation = await db.Queryable<ProjectManagementOperationEntity>().SingleAsync(item => item.Id == started.OperationId);
        Assert.Equal("Succeeded", operation.Status);
        Assert.Contains("DownloadReady", operation.ImpactJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exported.Count);
    }

    private static SqlSugarClient CreateDatabase()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-audit-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        return db;
    }

    private static async Task SeedAuditDataAsync(ISqlSugarClient db)
    {
        var now = DateTime.UtcNow;
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "project-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "VISIBLE", ProjectName = "Visible", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "project-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "HIDDEN", ProjectName = "Hidden", OwnerUserId = "other" }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity
        {
            Id = "visible-manager", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", UserId = "manager", RoleCode = "Manager", IsActive = true
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementActivityEntity { Id = "visible-success", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", AggregateType = "Task", AggregateId = "task-visible", ActivityType = "task.updated", Summary = "planning updated", TraceId = "trace-visible", ActorUserId = "manager", CreatedBy = "manager", CreatedTime = now, Remark = "{\"source\":\"User\"}" },
            new ProjectManagementActivityEntity { Id = "visible-failed", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", AggregateType = "Task", AggregateId = "task-failed", ActivityType = "task.failed", Summary = "更新失败", TraceId = "trace-failed", ActorUserId = "manager", CreatedBy = "manager", CreatedTime = now, Remark = "{\"source\":\"Governance\"}" },
            new ProjectManagementActivityEntity { Id = "hidden-match", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", AggregateType = "Task", AggregateId = "task-hidden", ActivityType = "task.updated", Summary = "planning updated", TraceId = "trace-hidden", ActorUserId = "manager", CreatedBy = "manager", CreatedTime = now, Remark = "{\"source\":\"User\"}" }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementSyncJournalEntity
        {
            Id = "journal-visible", TenantId = "tenant-a", AppCode = "SYSTEM", SequenceNo = 1, ProjectId = "project-visible", AggregateType = "Task", AggregateId = "task-visible", Operation = "updated", VersionNo = 1, PayloadJson = "{}", ActorUserId = "manager", DeviceId = "device-a", TraceId = "trace-visible", CreatedBy = "manager", CreatedTime = now
        }).ExecuteCommandAsync();
    }

    private static FixedAsterErpCurrentUser CreateUser(string userId) => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim(AsterErpClaimTypes.DataScope, "SELF")
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
        public List<ProjectManagementOperationJobArgs> Args { get; } = [];

        public Task<string> EnqueueAsync<TArgs>(TArgs args, BackgroundJobPriority priority = BackgroundJobPriority.Normal, TimeSpan? delay = null)
        {
            if (args is ProjectManagementOperationJobArgs operationArgs) Args.Add(operationArgs);
            return Task.FromResult(Guid.NewGuid().ToString("N"));
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "AsterERP.Api.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
