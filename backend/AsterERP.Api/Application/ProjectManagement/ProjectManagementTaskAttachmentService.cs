using System.Diagnostics;
using AsterERP.Api.Application.System.Files;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskAttachmentService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementFileStore fileStore,
    ProjectManagementAccessPolicy accessPolicy,
    IFileAppService fileAppService,
    IProjectManagementRealtimePublisher? realtimePublisher = null,
    IProjectManagementActivityWriter? activityWriter = null) : IProjectManagementTaskAttachmentService
{
    public async Task<IReadOnlyList<ProjectManagementTaskAttachmentResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskAttachmentEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.TaskId == task.Id && !item.IsDeleted).OrderBy(item => item.CreatedTime).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ProjectManagementTaskAttachmentResponse> UploadAsync(string taskId, IFormFile file, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        if (file is null || file.Length <= 0 || file.Length > 100 * 1024 * 1024) throw new ValidationException("附件大小必须在 1 字节到 100 MB 之间");
        var uploaded = await fileStore.StoreAsync(file, new ProjectManagementFileUploadContext(ProjectManagementFileWritePurpose.TaskAttachment, task.Id), cancellationToken);
        var entity = new ProjectManagementTaskAttachmentEntity
        {
            TenantId = Tenant(), AppCode = App(), ProjectId = task.ProjectId, TaskId = task.Id,
            FileId = uploaded.Id, FileName = uploaded.FileName, ContentType = file.ContentType ?? "application/octet-stream",
            FileSize = uploaded.Size, UploadedByUserId = User(), CreatedBy = User(), CreatedTime = DateTime.UtcNow
        };
        try
        {
            await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetCurrentDb(), async () =>
            {
                await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
                await WriteActivityAsync(task, entity, "attachment.created", $"上传附件 {entity.FileName}", CreateChanges(null, entity), entity.CreatedTime, cancellationToken);
            });
        }
        catch
        {
            try
            {
                await fileStore.DeleteAsync(uploaded.Id, CancellationToken.None);
            }
            catch
            {
                // 保留原始落库异常；文件清理由后续垃圾回收兜底。
            }

            throw;
        }

        await PublishInvalidationAsync(task, entity, "attachment.created", cancellationToken);
        return Map(entity);
    }

    public async Task<(ProjectManagementTaskAttachmentResponse Metadata, Stream Stream)> DownloadAsync(string taskId, string id, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        var entity = await FindAsync(task.Id, id, cancellationToken);
        return (Map(entity), await fileStore.OpenReadAsync(entity.FileId, cancellationToken));
    }

    public async Task<(ProjectManagementTaskAttachmentResponse Metadata, FilePreviewStreamResult Preview)> PreviewAsync(string taskId, string id, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        var entity = await FindAsync(task.Id, id, cancellationToken);
        var preview = await fileAppService.PreviewAsync(entity.FileId, cancellationToken);
        return (Map(entity), preview);
    }

    public async Task DeleteAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        var entity = await FindAsync(task.Id, id, cancellationToken);
        await accessPolicy.EnsureCanDeleteTaskAttachmentAsync(task.ProjectId, task.AssigneeUserId, entity.UploadedByUserId, cancellationToken);
        if (entity.VersionNo != versionNo) throw new ValidationException("附件已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        var before = AttachmentActivitySnapshot.From(entity);
        entity.IsDeleted = true; entity.DeletedBy = User(); entity.DeletedTime = DateTime.UtcNow; entity.UpdatedBy = User(); entity.UpdatedTime = entity.DeletedTime; entity.VersionNo++;
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetCurrentDb(), async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(task, entity, "attachment.deleted", $"删除附件 {entity.FileName}", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, cancellationToken);
        });
        await PublishInvalidationAsync(task, entity, "attachment.deleted", cancellationToken);
        // 附件是可恢复的业务对象；物理文件交由回收站/垃圾清理作业处理，避免数据库更新成功后文件删除失败导致状态不一致。
    }

    private async Task<ProjectManagementTaskEntity> GetTaskAsync(string taskId, CancellationToken cancellationToken)
        => (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == taskId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    private async Task<ProjectManagementTaskAttachmentEntity> FindAsync(string taskId, string id, CancellationToken cancellationToken)
        => (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && item.TaskId == taskId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("任务附件不存在", ErrorCodes.PlatformResourceNotFound);

    private async Task PublishInvalidationAsync(ProjectManagementTaskEntity task, ProjectManagementTaskAttachmentEntity attachment, string eventType, CancellationToken cancellationToken)
    {
        if (realtimePublisher is null) return;
        await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(Tenant(), App(), "TaskAttachment", attachment.Id, eventType, attachment.VersionNo, Guid.NewGuid().ToString("N"), task.ProjectId), cancellationToken);
    }
    private async Task WriteActivityAsync(
        ProjectManagementTaskEntity task,
        ProjectManagementTaskAttachmentEntity attachment,
        string activityType,
        string summary,
        IReadOnlyList<ProjectManagementActivityFieldChange> changes,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            Tenant(), App(), "TaskAttachment", attachment.Id, activityType, summary,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), User(), task.ProjectId,
            Source: "User", FieldChanges: changes, OccurredAt: occurredAt), cancellationToken);
    }
    private static IReadOnlyList<ProjectManagementActivityFieldChange> CreateChanges(AttachmentActivitySnapshot? before, ProjectManagementTaskAttachmentEntity after) =>
        ProjectManagementActivityChanges.Collect(
            ProjectManagementActivityChanges.Create("FileName", "附件名称", before?.FileName, after.FileName),
            ProjectManagementActivityChanges.Create("ContentType", "文件类型", before?.ContentType, after.ContentType),
            ProjectManagementActivityChanges.Create("FileSize", "文件大小", before?.FileSize, after.FileSize),
            ProjectManagementActivityChanges.Create("IsDeleted", "已删除", before?.IsDeleted, after.IsDeleted));
    private sealed record AttachmentActivitySnapshot(string FileName, string ContentType, long FileSize, bool IsDeleted)
    {
        public static AttachmentActivitySnapshot From(ProjectManagementTaskAttachmentEntity entity) => new(entity.FileName, entity.ContentType, entity.FileSize, entity.IsDeleted);
    }
    private ProjectManagementTaskAttachmentResponse Map(ProjectManagementTaskAttachmentEntity item)
    {
        var format = FilePreviewFormatCatalog.Resolve(FilePreviewFormatCatalog.NormalizeExtensionFromFileName(item.FileName));
        return new(
            item.Id,
            item.ProjectId,
            item.TaskId,
            item.FileId,
            item.FileName,
            item.ContentType,
            item.FileSize,
            $"/api/project-management/tasks/{Uri.EscapeDataString(item.TaskId)}/attachments/{Uri.EscapeDataString(item.Id)}/download",
            $"/api/project-management/tasks/{Uri.EscapeDataString(item.TaskId)}/attachments/{Uri.EscapeDataString(item.Id)}/preview",
            item.UploadedByUserId,
            item.CreatedTime,
            item.VersionNo,
            format is not null,
            format?.ViewerType,
            format?.PreviewPipeline);
    }
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
}
