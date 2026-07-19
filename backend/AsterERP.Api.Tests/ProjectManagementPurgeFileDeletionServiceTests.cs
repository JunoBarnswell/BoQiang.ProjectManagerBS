using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementPurgeFileDeletionServiceTests
{
    [Fact]
    public async Task Schedule_counts_workspace_references_before_queuing_file_deletion()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new[]
        {
            new ProjectManagementTaskAttachmentEntity { Id = "target-shared", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-deleted", FileId = "file-shared", FileName = "shared.txt" },
            new ProjectManagementTaskAttachmentEntity { Id = "other-shared", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-kept", FileId = "file-shared", FileName = "shared.txt" },
            new ProjectManagementTaskAttachmentEntity { Id = "target-exclusive", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-deleted", FileId = "file-exclusive", FileName = "exclusive.txt" }
        }).ExecuteCommandAsync();

        var service = new ProjectManagementPurgeFileDeletionService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), new RecordingFileStore());
        var targets = await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.TaskId == "task-deleted").ToListAsync();
        await service.ScheduleAsync(db, "purge-1", targets);

        var pending = await db.Queryable<ProjectManagementPurgeFileDeletionEntity>().ToListAsync();
        Assert.Equal(["file-exclusive"], pending.Select(item => item.FileId));
    }

    [Fact]
    public async Task Missing_file_is_idempotently_completed_and_transient_failure_is_retryable()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new[]
        {
            new ProjectManagementPurgeFileDeletionEntity { Id = "missing-row", TenantId = "tenant-a", AppCode = "SYSTEM", OperationId = "purge-missing", FileId = "missing", Status = "Pending", CreatedTime = DateTime.UtcNow.AddMinutes(-2) },
            new ProjectManagementPurgeFileDeletionEntity { Id = "retry-row", TenantId = "tenant-a", AppCode = "SYSTEM", OperationId = "purge-retry", FileId = "retry", Status = "Pending", CreatedTime = DateTime.UtcNow.AddMinutes(-1) }
        }).ExecuteCommandAsync();

        var fileStore = new RecordingFileStore { MissingFileIds = ["missing"], FailuresRemaining = 1 };
        var service = new ProjectManagementPurgeFileDeletionService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), fileStore);

        await service.TryProcessAsync("purge-missing");
        var missing = await db.Queryable<ProjectManagementPurgeFileDeletionEntity>().SingleAsync(item => item.Id == "missing-row");
        Assert.Equal("Completed", missing.Status);
        Assert.Equal(1, missing.AttemptCount);
        Assert.Contains("不存在", missing.LastError);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TryProcessAsync("purge-retry"));
        var pending = await db.Queryable<ProjectManagementPurgeFileDeletionEntity>().SingleAsync(item => item.Id == "retry-row");
        Assert.Equal("Pending", pending.Status);
        Assert.Equal(1, pending.AttemptCount);

        await service.TryProcessAsync("purge-retry");
        var completed = await db.Queryable<ProjectManagementPurgeFileDeletionEntity>().SingleAsync(item => item.Id == "retry-row");
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(2, completed.AttemptCount);
    }

    private static SqlSugarClient CreateDb() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:pm-purge-file-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
    ], "test")));

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class RecordingFileStore : IProjectManagementFileStore
    {
        public HashSet<string> MissingFileIds { get; set; } = [];
        public int FailuresRemaining { get; set; }

        public Task<ProjectManagementStoredFile> StoreAsync(IFormFile file, ProjectManagementFileUploadContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(string fileId, CancellationToken cancellationToken = default)
        {
            if (MissingFileIds.Contains(fileId)) throw new NotFoundException("文件不存在", ErrorCodes.FileNotFound);
            if (FailuresRemaining-- > 0) throw new InvalidOperationException("temporary storage failure");
            return Task.CompletedTask;
        }
    }
}
