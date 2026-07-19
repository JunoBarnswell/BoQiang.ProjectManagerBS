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
    IFileAppService fileAppService,
    ProjectManagementAccessPolicy accessPolicy,
    IProjectManagementRealtimePublisher? realtimePublisher = null) : IProjectManagementTaskAttachmentService
{
    public async Task<IReadOnlyList<ProjectManagementTaskAttachmentResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        var rows = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementTaskAttachmentEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.TaskId == task.Id && !item.IsDeleted).OrderBy(item => item.CreatedTime).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ProjectManagementTaskAttachmentResponse> UploadAsync(string taskId, IFormFile file, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        if (file is null || file.Length <= 0 || file.Length > 100 * 1024 * 1024) throw new ValidationException("附件大小必须在 1 字节到 100 MB 之间");
        var uploaded = await fileAppService.UploadAsync(file, $"ProjectManagement task:{task.Id}", cancellationToken);
        var entity = new ProjectManagementTaskAttachmentEntity
        {
            TenantId = Tenant(), AppCode = App(), ProjectId = task.ProjectId, TaskId = task.Id,
            FileId = uploaded.Id, FileName = uploaded.FileName, ContentType = file.ContentType ?? "application/octet-stream",
            FileSize = uploaded.Size, UploadedByUserId = User(), CreatedBy = User(), CreatedTime = DateTime.UtcNow
        };
        try
        {
            await databaseAccessor.GetProjectManagementDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await fileAppService.DeleteAsync(uploaded.Id, CancellationToken.None);
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
        var downloaded = await fileAppService.DownloadAsync(entity.FileId, cancellationToken);
        return (Map(entity), downloaded.Stream);
    }

    public async Task DeleteAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        var entity = await FindAsync(task.Id, id, cancellationToken);
        if (entity.VersionNo != versionNo) throw new ValidationException("附件已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        entity.IsDeleted = true; entity.DeletedBy = User(); entity.DeletedTime = DateTime.UtcNow; entity.UpdatedBy = User(); entity.UpdatedTime = entity.DeletedTime; entity.VersionNo++;
        await databaseAccessor.GetProjectManagementDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await PublishInvalidationAsync(task, entity, "attachment.deleted", cancellationToken);
        // 附件是可恢复的业务对象；物理文件交由回收站/垃圾清理作业处理，避免数据库更新成功后文件删除失败导致状态不一致。
    }

    private async Task<ProjectManagementTaskEntity> GetTaskAsync(string taskId, CancellationToken cancellationToken)
        => (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == taskId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    private async Task<ProjectManagementTaskAttachmentEntity> FindAsync(string taskId, string id, CancellationToken cancellationToken)
        => (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && item.TaskId == taskId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("任务附件不存在", ErrorCodes.PlatformResourceNotFound);

    private async Task PublishInvalidationAsync(ProjectManagementTaskEntity task, ProjectManagementTaskAttachmentEntity attachment, string eventType, CancellationToken cancellationToken)
    {
        if (realtimePublisher is null) return;
        await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(Tenant(), App(), "TaskAttachment", attachment.Id, eventType, attachment.VersionNo, Guid.NewGuid().ToString("N"), task.ProjectId), cancellationToken);
    }
    private ProjectManagementTaskAttachmentResponse Map(ProjectManagementTaskAttachmentEntity item) => new(item.Id, item.ProjectId, item.TaskId, item.FileId, item.FileName, item.ContentType, item.FileSize, $"/api/project-management/tasks/{item.TaskId}/attachments/{item.Id}/download", $"/api/system/files/{Uri.EscapeDataString(item.FileId)}/preview", item.UploadedByUserId, item.CreatedTime, item.VersionNo);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private static string App() => ProjectManagementPlatformScope.AppCode;
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
}
