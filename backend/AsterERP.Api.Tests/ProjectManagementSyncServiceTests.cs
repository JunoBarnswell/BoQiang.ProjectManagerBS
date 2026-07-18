using System.Security.Claims;
using System.IO.Compression;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementSyncServiceTests
{
    [Fact]
    public async Task Export_and_preview_validate_workspace_checksum_and_conflicts()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-sync-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        db.CodeFirst.InitTables<SystemUserEntity>();
        await db.Insertable(new SystemUserEntity { Id = "operator", UserName = "operator", PasswordHash = "secret", Status = "Enabled" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-sync", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "SYNC", ProjectName = "Sync", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity
        {
            Id = "task-sync", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-sync", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();

        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser();
        var service = new ProjectManagementSyncService(accessor, user, new ProjectManagementAccessPolicy(accessor, user), new TestPasswordHashService());
        var exported = await service.ExportAsync(new ProjectManagementSyncExportRequest("project-sync", DeviceId: "device-a"));
        Assert.EndsWith(".bqsync", exported.FileName, StringComparison.Ordinal);

        await using var package = new MemoryStream(exported.Content);
        var preview = await service.PreviewAsync(package);
        Assert.True(preview.IsCompatible);
        Assert.Equal("tenant-a", preview.TenantId);
        Assert.Equal(1, preview.ProjectCount);
        Assert.Equal(1, preview.TaskCount);
        Assert.Contains(preview.Conflicts, item => item.StartsWith("Project:project-sync:", StringComparison.Ordinal));

        var journalWriter = new ProjectManagementSyncJournalWriter(accessor);
        await journalWriter.AppendAsync(new ProjectManagementSyncJournalEvent(
            "tenant-a", "MES", "Task", "task-sync", "project-sync", "updated", 2,
            "{\"id\":\"task-sync\",\"versionNo\":2}", "operator", "device-a", "trace-sync"));
        var changes = await service.GetChangesAsync("project-sync", 0, 20);
        Assert.Single(changes);
        Assert.Equal(1, changes[0].SequenceNo);
        var acknowledged = await service.AcknowledgeAsync(new ProjectManagementSyncAcknowledgeRequest("device-a", 1));
        Assert.Equal(1, acknowledged.AcknowledgedSequenceNo);
        var watermark = await service.GetWatermarkAsync("device-a");
        Assert.Equal(1, watermark.AcknowledgedSequenceNo);

        var imported = await service.ImportAsync(
            new MemoryStream(exported.Content),
            new ProjectManagementSyncImportRequest("secret", ConfirmRisk: true, ConflictStrategy: "Skip"));
        Assert.Equal(0, imported.Inserted);
        Assert.True(imported.Skipped >= 2);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.ImportAsync(
            new MemoryStream(exported.Content),
            new ProjectManagementSyncImportRequest("secret", ConfirmRisk: true, ConflictStrategy: "Reject")));

        var tampered = exported.Content.ToArray();
        tampered[^1] ^= 0xFF;
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.PreviewAsync(new MemoryStream(tampered)));
    }

    [Fact]
    public async Task Sync_journal_assigns_monotonic_workspace_watermarks()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-journal-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);

        var writer = new ProjectManagementSyncJournalWriter(new TestWorkspaceDatabaseAccessor(db));
        await writer.AppendAsync(new ProjectManagementSyncJournalEvent(
            "tenant-a", "MES", "Task", "task-1", "project-1", "created", 1,
            "{\"id\":\"task-1\"}", "operator", "device-a", "trace-1"));
        await writer.AppendAsync(new ProjectManagementSyncJournalEvent(
            "tenant-a", "MES", "Task", "task-1", "project-1", "updated", 2,
            "{\"id\":\"task-1\",\"version\":2}", "operator", "device-a", "trace-2"));

        var rows = await db.Queryable<ProjectManagementSyncJournalEntity>()
            .OrderBy(item => item.SequenceNo, OrderByType.Asc)
            .ToListAsync();
        Assert.Equal(new long[] { 1, 2 }, rows.Select(item => item.SequenceNo).ToArray());
        Assert.Equal("updated", rows[1].Operation);
        Assert.Equal("device-a", rows[1].DeviceId);
    }

    [Fact]
    public async Task Sync_watermark_accepts_zero_rejects_negative_and_future_values()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-sync-watermark-boundary-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "watermark-project", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "WATERMARK", ProjectName = "Watermark", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity
        {
            Id = "watermark-member", TenantId = "tenant-a", AppCode = "MES", ProjectId = "watermark-project", UserId = "operator", RoleCode = "Owner", IsActive = true
        }).ExecuteCommandAsync();
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser();
        var service = new ProjectManagementSyncService(accessor, user, new ProjectManagementAccessPolicy(accessor, user), new TestPasswordHashService());

        var zero = await service.AcknowledgeAsync(new ProjectManagementSyncAcknowledgeRequest("device-boundary", 0));
        Assert.Equal(0, zero.AcknowledgedSequenceNo);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.AcknowledgeAsync(new ProjectManagementSyncAcknowledgeRequest("device-boundary", -1)));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.AcknowledgeAsync(new ProjectManagementSyncAcknowledgeRequest("device-boundary", 1)));
    }

    [Fact]
    public async Task Sync_preview_rejects_zip_entries_that_escape_the_archive_root()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-sync-path-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser();
        var service = new ProjectManagementSyncService(accessor, user, new ProjectManagementAccessPolicy(accessor, user), new TestPasswordHashService());
        await using var package = new MemoryStream();
        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using var entry = archive.CreateEntry("../outside.json").Open();
            await entry.WriteAsync(new byte[] { 1 }, CancellationToken.None);
        }
        package.Position = 0;

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.PreviewAsync(package));
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "MES")
    }, "test")));

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class TestPasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => password;
        public PasswordVerificationResult Verify(string storedPassword, string inputPassword) => new(true, false, "Success", "test");
    }
}
