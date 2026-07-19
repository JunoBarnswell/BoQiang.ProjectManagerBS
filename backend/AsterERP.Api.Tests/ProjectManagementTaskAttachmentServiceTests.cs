using System.Reflection;
using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Application.System.Files;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.System.Files;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskAttachmentServiceTests
{
    [Fact]
    public async Task Attachment_list_download_and_preview_are_task_scoped_and_preserve_metadata()
    {
        using var db = CreateDb("list-preview");
        await SeedTaskAsync(db);
        await db.Insertable(new ProjectManagementTaskAttachmentEntity
        {
            Id = "attachment-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a",
            FileId = "file-a", FileName = "方案 1.pdf", ContentType = "application/pdf", FileSize = 42,
            UploadedByUserId = "operator", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();

        var fileApp = new RecordingFileAppService(new FilePreviewStreamResult("方案 1.pdf", "application/pdf", 42, new MemoryStream([1, 2, 3])));
        var service = CreateService(db, CreateUser(), fileApp);

        var attachments = await service.QueryAsync("task-a");
        var attachment = Assert.Single(attachments);
        Assert.Equal("方案 1.pdf", attachment.FileName);
        Assert.Equal("/api/project-management/tasks/task-a/attachments/attachment-a/download", attachment.DownloadUrl);
        Assert.Equal("/api/project-management/tasks/task-a/attachments/attachment-a/preview", attachment.PreviewUrl);
        Assert.True(attachment.PreviewSupported);
        Assert.Equal("pdf", attachment.PreviewType);

        var preview = await service.PreviewAsync("task-a", "attachment-a");
        Assert.Equal("file-a", fileApp.LastPreviewedFileId);
        Assert.Equal("方案 1.pdf", preview.Preview.FileName);
        Assert.Equal(42, preview.Metadata.FileSize);

        var download = await service.DownloadAsync("task-a", "attachment-a");
        Assert.Equal("方案 1.pdf", download.Metadata.FileName);
        await download.Stream.DisposeAsync();
    }

    [Fact]
    public async Task Attachment_query_has_empty_state_and_preview_reports_unsupported_or_missing_files()
    {
        using var db = CreateDb("errors");
        await SeedTaskAsync(db);
        var fileApp = new RecordingFileAppService(null);
        var service = CreateService(db, CreateUser(), fileApp);

        Assert.Empty(await service.QueryAsync("task-a"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.PreviewAsync("task-a", "missing"));

        await db.Insertable(new ProjectManagementTaskAttachmentEntity
        {
            Id = "attachment-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a",
            FileId = "file-a", FileName = "archive.bin", ContentType = "application/octet-stream", FileSize = 8,
            UploadedByUserId = "operator", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
        fileApp.PreviewException = new ValidationException("当前文件格式不支持预览");

        var attachment = Assert.Single(await service.QueryAsync("task-a"));
        Assert.False(attachment.PreviewSupported);
        await Assert.ThrowsAsync<ValidationException>(() => service.PreviewAsync("task-a", "attachment-a"));
    }

    [Fact]
    public async Task Attachment_list_and_preview_require_project_visibility()
    {
        using var db = CreateDb("permission");
        await SeedTaskAsync(db);
        var service = CreateService(db, CreateUser("outsider"), new RecordingFileAppService(null));

        await Assert.ThrowsAsync<ValidationException>(() => service.QueryAsync("task-a"));
        await Assert.ThrowsAsync<ValidationException>(() => service.PreviewAsync("task-a", "attachment-a"));
    }

    [Fact]
    public async Task Attachment_delete_allows_uploader_assignee_owner_and_manager_but_not_unrelated_lead()
    {
        using var db = CreateDb("delete-policy");
        await SeedTaskAsync(db);
        await db.Updateable<ProjectManagementTaskEntity>()
            .SetColumns(item => new ProjectManagementTaskEntity { AssigneeUserId = "assignee" })
            .Where(item => item.Id == "task-a")
            .ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementProjectMemberEntity { Id = "member-uploader", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "uploader", RoleCode = "Member", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-assignee", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "assignee", RoleCode = "Member", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-manager", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "manager", RoleCode = "Manager", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-lead", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "lead", RoleCode = "Lead", IsActive = true }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            Attachment("attachment-uploader", "uploader"),
            Attachment("attachment-assignee", "other"),
            Attachment("attachment-manager", "other"),
            Attachment("attachment-owner", "other"),
            Attachment("attachment-lead", "other")
        }).ExecuteCommandAsync();

        var activityWriter = new RecordingActivityWriter();
        await CreateService(db, CreateUser("uploader"), new RecordingFileAppService(null), activityWriter: activityWriter).DeleteAsync("task-a", "attachment-uploader", 1);
        await CreateService(db, CreateUser("assignee"), new RecordingFileAppService(null), activityWriter: activityWriter).DeleteAsync("task-a", "attachment-assignee", 1);
        await CreateService(db, CreateUser("manager"), new RecordingFileAppService(null), activityWriter: activityWriter).DeleteAsync("task-a", "attachment-manager", 1);
        await CreateService(db, CreateUser("operator"), new RecordingFileAppService(null), activityWriter: activityWriter).DeleteAsync("task-a", "attachment-owner", 1);
        await Assert.ThrowsAsync<ValidationException>(() => CreateService(db, CreateUser("lead"), new RecordingFileAppService(null), activityWriter: activityWriter).DeleteAsync("task-a", "attachment-lead", 1));

        Assert.Equal(4, activityWriter.Events.Count(item => item.ActivityType == "attachment.deleted"));
        Assert.True(await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.Id == "attachment-lead").AnyAsync(item => !item.IsDeleted));
    }

    [Fact]
    public async Task Attachment_upload_activity_failure_compensates_storage_and_database()
    {
        using var db = CreateDb("upload-audit-failure");
        await SeedTaskAsync(db);
        var activityWriter = new RecordingActivityWriter { Fail = true };
        var fileStore = new RecordingFileStore();
        var service = CreateService(db, CreateUser(), new RecordingFileAppService(null), fileStore, activityWriter);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync("task-a", CreateFormFile("证据.txt")));

        Assert.Contains("file-upload", fileStore.DeletedFileIds);
        Assert.Empty(await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.TaskId == "task-a").ToListAsync());
    }

    [Fact]
    public async Task Attachment_upload_cleanup_failure_schedules_audited_orphan_retry()
    {
        using var db = CreateDb("upload-orphan");
        await SeedTaskAsync(db);
        var fileStore = new RecordingFileStore { FailDelete = true };
        var activityWriter = new RecordingActivityWriter { Fail = true };
        var orphanWriter = new RecordingOperationWriter();
        var orphanDeletes = new RecordingPurgeFileDeletionService();
        var jobs = new RecordingBackgroundJobManager();
        var service = CreateService(db, CreateUser(), new RecordingFileAppService(null), fileStore, activityWriter, orphanDeletes, orphanWriter, jobs);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync("task-a", CreateFormFile("孤儿.txt")));

        Assert.Equal("attachment.orphan-cleanup", orphanWriter.OperationType);
        Assert.Contains("file-upload", orphanDeletes.FileIds);
        Assert.Single(jobs.Args);
        Assert.Equal(orphanWriter.OperationId, jobs.Args[0].OperationId);
    }

    [Fact]
    public async Task Attachment_access_is_revoked_after_member_removal_and_file_id_cannot_cross_task_boundary()
    {
        using var db = CreateDb("access-revocation");
        await SeedTaskAsync(db);
        await db.Insertable(new ProjectManagementProjectMemberEntity
        {
            Id = "member-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "member-a", RoleCode = "Member", IsActive = true
        }).ExecuteCommandAsync();
        await db.Insertable(Attachment("attachment-a", "member-a")).ExecuteCommandAsync();
        var fileStore = new RecordingFileStore();
        var memberService = CreateService(db, CreateUser("member-a"), new RecordingFileAppService(null), fileStore);

        Assert.Single(await memberService.QueryAsync("task-a"));
        await db.Updateable<ProjectManagementProjectMemberEntity>()
            .SetColumns(item => new ProjectManagementProjectMemberEntity { IsActive = false, IsDeleted = true })
            .Where(item => item.Id == "member-a")
            .ExecuteCommandAsync();
        await Assert.ThrowsAsync<ValidationException>(() => memberService.DownloadAsync("task-a", "attachment-a"));
        Assert.Equal(0, fileStore.OpenReadCount);

        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "B", ProjectName = "B", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-b", TaskCode = "B-1", Title = "B" }).ExecuteCommandAsync();
        await db.Insertable(Attachment("attachment-b", "operator", "task-b", "project-b", "file-b")).ExecuteCommandAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(db, CreateUser(), new RecordingFileAppService(null)).DownloadAsync("task-a", "attachment-b"));
    }

    [Fact]
    public void Attachment_controller_splits_view_and_manage_permissions_by_operation()
    {
        var controller = typeof(ProjectManagementTaskAttachmentsController);
        Assert.Empty(controller.GetCustomAttributes<PermissionAttribute>());
        Assert.Equal(PermissionCodes.ProjectManagementTaskView, controller.GetMethod(nameof(ProjectManagementTaskAttachmentsController.QueryAsync))?.GetCustomAttributes<PermissionAttribute>().Single().Code);
        Assert.Equal(PermissionCodes.ProjectManagementAttachmentManage, controller.GetMethod(nameof(ProjectManagementTaskAttachmentsController.UploadAsync))?.GetCustomAttributes<PermissionAttribute>().Single().Code);
        Assert.Equal(PermissionCodes.ProjectManagementTaskView, controller.GetMethod(nameof(ProjectManagementTaskAttachmentsController.DownloadAsync))?.GetCustomAttributes<PermissionAttribute>().Single().Code);
        Assert.Equal(PermissionCodes.ProjectManagementTaskView, controller.GetMethod(nameof(ProjectManagementTaskAttachmentsController.PreviewAsync))?.GetCustomAttributes<PermissionAttribute>().Single().Code);
        Assert.Equal(PermissionCodes.ProjectManagementAttachmentManage, controller.GetMethod(nameof(ProjectManagementTaskAttachmentsController.DeleteAsync))?.GetCustomAttributes<PermissionAttribute>().Single().Code);
    }

    private static ProjectManagementTaskAttachmentService CreateService(
        ISqlSugarClient db,
        ICurrentUser user,
        RecordingFileAppService fileApp,
        RecordingFileStore? fileStore = null,
        RecordingActivityWriter? activityWriter = null,
        IProjectManagementPurgeFileDeletionService? purgeFileDeletionService = null,
        IProjectManagementOperationWriter? operationWriter = null,
        IBackgroundJobManager? backgroundJobManager = null) =>
        new(new TestWorkspaceDatabaseAccessor(db), user, fileStore ?? new RecordingFileStore(), new ProjectManagementAccessPolicy(new TestWorkspaceDatabaseAccessor(db), user), fileApp, null, activityWriter, purgeFileDeletionService, operationWriter, backgroundJobManager);

    private static ProjectManagementTaskAttachmentEntity Attachment(string id, string uploadedByUserId, string taskId = "task-a", string projectId = "project-a", string? fileId = null) => new()
    {
        Id = id, TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = projectId, TaskId = taskId,
        FileId = fileId ?? $"file-{id}", FileName = $"{id}.txt", ContentType = "text/plain", FileSize = 8,
        UploadedByUserId = uploadedByUserId, CreatedBy = uploadedByUserId, CreatedTime = DateTime.UtcNow
    };

    private static FormFile CreateFormFile(string fileName)
    {
        var stream = new MemoryStream([1, 2, 3]);
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
    }

    private static async Task SeedTaskAsync(ISqlSugarClient db)
    {
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity
        {
            Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
    }

    private static SqlSugarClient CreateDb(string name) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-attachments-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser(string userId = "operator") => new(new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(AsterErpClaimTypes.UserId, userId),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
    ], "test")));

    private sealed class RecordingFileStore : IProjectManagementFileStore
    {
        public List<string> DeletedFileIds { get; } = [];
        public int OpenReadCount { get; private set; }
        public bool FailDelete { get; set; }

        public Task<ProjectManagementStoredFile> StoreAsync(IFormFile file, ProjectManagementFileUploadContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectManagementStoredFile("file-upload", file.FileName, file.Length));

        public Task<Stream> OpenReadAsync(string fileId, CancellationToken cancellationToken = default)
        {
            OpenReadCount++;
            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        }

        public Task DeleteAsync(string fileId, CancellationToken cancellationToken = default)
        {
            if (FailDelete) throw new InvalidOperationException("storage delete failed");
            DeletedFileIds.Add(fileId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingActivityWriter : IProjectManagementActivityWriter
    {
        public List<ProjectManagementActivityEvent> Events { get; } = [];
        public bool Fail { get; set; }

        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new InvalidOperationException("activity writer failed");
            Events.Add(activity);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPurgeFileDeletionService : IProjectManagementPurgeFileDeletionService
    {
        public List<string> FileIds { get; } = [];
        public Task ScheduleAsync(ISqlSugarClient db, string operationId, IReadOnlyCollection<ProjectManagementTaskAttachmentEntity> attachments, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ScheduleOrphanAsync(ISqlSugarClient db, string operationId, string fileId, CancellationToken cancellationToken = default)
        {
            FileIds.Add(fileId);
            return Task.CompletedTask;
        }
        public Task<bool> TryProcessAsync(string operationId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class RecordingOperationWriter : IProjectManagementOperationWriter
    {
        public string? OperationId { get; private set; }
        public string? OperationType { get; private set; }
        public Task CreatePendingAsync(string operationId, string operationType, string impactJson, string traceId, CancellationToken cancellationToken = default)
        {
            OperationId = operationId;
            OperationType = operationType;
            return Task.CompletedTask;
        }
        public Task StartAsync(string operationId, string operationType, string impactJson, string traceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ReportProgressAsync(string operationId, string phase, int progressPercent, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> IsCancellationRequestedAsync(string operationId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RequestCancellationAsync(string operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CancelAsync(string operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SucceedAsync(string operationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CompleteWithImpactAsync(string operationId, string impactJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task FailAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task FailRunningExceptAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

    private sealed class RecordingFileAppService(FilePreviewStreamResult? preview) : IFileAppService
    {
        public string? LastPreviewedFileId { get; private set; }
        public Exception? PreviewException { get; set; }

        public Task<FilePreviewStreamResult> PreviewAsync(string id, CancellationToken cancellationToken = default)
        {
            LastPreviewedFileId = id;
            if (PreviewException is not null) return Task.FromException<FilePreviewStreamResult>(PreviewException);
            return preview is null ? Task.FromException<FilePreviewStreamResult>(new NotFoundException("文件不存在", ErrorCodes.FileNotFound)) : Task.FromResult(preview);
        }

        public Task<GridPageResult<FileRecordResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<FileRecordResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<FilePreviewFormatResponse>> GetPreviewFormatsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<FileUploadResponse> UploadAsync(IFormFile file, string? remark, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(FileRecordResponse Metadata, Stream Stream)> DownloadAsync(string id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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
