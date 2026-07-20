using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using System.Text.Json;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskDraftService(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser, ProjectManagementAccessPolicy accessPolicy, IProjectManagementFileStore fileStore) : IProjectManagementTaskDraftService
{
    public async Task<ProjectManagementTaskDraftResponse> CreateAsync(ProjectManagementTaskDraftCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId)) throw new ValidationException("项目不能为空");
        await accessPolicy.EnsureCanViewProjectAsync(request.ProjectId, cancellationToken);
        if (request.ExpiresInHours is < 1 or > 168) throw new ValidationException("草稿有效期必须在 1 到 168 小时之间");
        try { JsonDocument.Parse(request.PayloadJson); } catch (JsonException) { throw new ValidationException("草稿内容不是有效 JSON"); }
        var entity = new ProjectManagementTaskDraftEntity { TenantId = Tenant(), AppCode = App(), ProjectId = request.ProjectId, OwnerUserId = User(), PayloadJson = request.PayloadJson, ExpiresAt = DateTime.UtcNow.AddHours(request.ExpiresInHours), CreatedBy = User(), CreatedTime = DateTime.UtcNow };
        await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ProjectManagementTaskDraftResponse> GetAsync(string id, CancellationToken cancellationToken = default) => Map(await GetDraftAsync(id, cancellationToken));

    public async Task BindAsync(string id, string taskId, string projectId, CancellationToken cancellationToken = default)
    {
        var draft = await GetDraftAsync(id, cancellationToken);
        if (!string.Equals(draft.ProjectId, projectId, StringComparison.Ordinal)) throw new ValidationException("草稿与需求项目不一致");
        var db = databaseAccessor.GetCurrentDb();
        var attachments = await db.Queryable<ProjectManagementTaskDraftAttachmentEntity>().Where(item => item.DraftId == draft.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        foreach (var attachment in attachments)
        {
            await db.Insertable(new ProjectManagementTaskAttachmentEntity
            {
                TenantId = attachment.TenantId, AppCode = attachment.AppCode, ProjectId = projectId, TaskId = taskId,
                FileId = attachment.FileId, FileName = attachment.FileName, ContentType = attachment.ContentType, FileSize = attachment.FileSize,
                UploadedByUserId = attachment.UploadedByUserId, CreatedBy = User(), CreatedTime = DateTime.UtcNow, VersionNo = 1,
            }).ExecuteCommandAsync(cancellationToken);
            attachment.IsDeleted = true; attachment.DeletedBy = User(); attachment.DeletedTime = DateTime.UtcNow; attachment.UpdatedBy = User(); attachment.UpdatedTime = attachment.DeletedTime; attachment.VersionNo++;
        }
        draft.IsDeleted = true; draft.DeletedBy = User(); draft.DeletedTime = DateTime.UtcNow; draft.UpdatedBy = User(); draft.UpdatedTime = draft.DeletedTime; draft.VersionNo++;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(draft).ExecuteCommandAsync(cancellationToken);
            if (attachments.Count > 0) await db.Updateable(attachments).ExecuteCommandAsync(cancellationToken);
        });
    }

    public async Task<ProjectManagementTaskDraftAttachmentResponse> UploadAsync(string id, IFormFile file, CancellationToken cancellationToken = default)
    {
        var draft = await GetDraftAsync(id, cancellationToken);
        if (file is null || file.Length <= 0 || file.Length > 100 * 1024 * 1024) throw new ValidationException("附件大小必须在 1 字节到 100 MB 之间");
        var stored = await fileStore.StoreAsync(file, new ProjectManagementFileUploadContext(ProjectManagementFileWritePurpose.TaskAttachment), cancellationToken);
        var entity = new ProjectManagementTaskDraftAttachmentEntity { TenantId = Tenant(), AppCode = App(), ProjectId = draft.ProjectId, DraftId = draft.Id, FileId = stored.Id, FileName = stored.FileName, ContentType = file.ContentType ?? "application/octet-stream", FileSize = stored.Size, UploadedByUserId = User(), CreatedBy = User(), CreatedTime = DateTime.UtcNow };
        try { await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken); } catch { await fileStore.DeleteAsync(stored.Id, CancellationToken.None); throw; }
        return new(entity.Id, entity.DraftId, entity.FileName, entity.ContentType, entity.FileSize, entity.VersionNo, entity.CreatedTime);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var draft = await GetDraftAsync(id, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var attachments = await db.Queryable<ProjectManagementTaskDraftAttachmentEntity>().Where(item => item.DraftId == draft.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        draft.IsDeleted = true; draft.DeletedBy = User(); draft.DeletedTime = DateTime.UtcNow; draft.UpdatedBy = User(); draft.UpdatedTime = draft.DeletedTime; draft.VersionNo++;
        foreach (var attachment in attachments) { attachment.IsDeleted = true; attachment.DeletedBy = User(); attachment.DeletedTime = draft.DeletedTime; attachment.UpdatedBy = User(); attachment.UpdatedTime = draft.DeletedTime; attachment.VersionNo++; }
        await ProjectManagementMutationTransaction.RunAsync(db, async () => { await db.Updateable(draft).ExecuteCommandAsync(cancellationToken); if (attachments.Count > 0) await db.Updateable(attachments).ExecuteCommandAsync(cancellationToken); });
        foreach (var attachment in attachments) { try { await fileStore.DeleteAsync(attachment.FileId, CancellationToken.None); } catch { } }
    }

    private async Task<ProjectManagementTaskDraftEntity> GetDraftAsync(string id, CancellationToken cancellationToken)
    {
        var draft = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskDraftEntity>().Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && item.OwnerUserId == User() && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("需求草稿不存在", ErrorCodes.PlatformResourceNotFound);
        if (draft.ExpiresAt <= DateTime.UtcNow) throw new ValidationException("需求草稿已过期");
        await accessPolicy.EnsureCanViewProjectAsync(draft.ProjectId, cancellationToken);
        return draft;
    }
    private static ProjectManagementTaskDraftResponse Map(ProjectManagementTaskDraftEntity item) => new(item.Id, item.ProjectId, item.PayloadJson, item.ExpiresAt, item.VersionNo);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
}
