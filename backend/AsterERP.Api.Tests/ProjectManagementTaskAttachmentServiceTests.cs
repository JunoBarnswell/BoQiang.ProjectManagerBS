using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Application.System.Files;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.System.Files;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using SqlSugar;
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

    private static ProjectManagementTaskAttachmentService CreateService(ISqlSugarClient db, ICurrentUser user, RecordingFileAppService fileApp) =>
        new(new TestWorkspaceDatabaseAccessor(db), user, new RecordingFileStore(), new ProjectManagementAccessPolicy(new TestWorkspaceDatabaseAccessor(db), user), fileApp);

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
        public Task<ProjectManagementStoredFile> StoreAsync(IFormFile file, ProjectManagementFileUploadContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectManagementStoredFile("file-upload", file.FileName, file.Length));

        public Task<Stream> OpenReadAsync(string fileId, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));

        public Task DeleteAsync(string fileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
