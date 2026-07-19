using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>将外部 Blob 删除作为数据库提交后的可靠作业，避免事务回滚时提前丢失文件。</summary>
public sealed class ProjectManagementPurgeFileDeletionService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementFileStore fileStore) : IProjectManagementPurgeFileDeletionService, IScopedDependency
{
    public async Task ScheduleAsync(ISqlSugarClient db, string operationId, IReadOnlyCollection<ProjectManagementTaskAttachmentEntity> attachments, CancellationToken cancellationToken = default)
    {
        var candidates = attachments
            .Where(item => !string.IsNullOrWhiteSpace(item.FileId))
            .Select(item => new { item.Id, FileId = item.FileId.Trim() })
            .ToList();
        if (candidates.Count == 0) return;

        var fileIds = candidates.Select(item => item.FileId).Distinct(StringComparer.Ordinal).ToList();
        var attachmentIds = candidates.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToList();
        var referencedFileIds = await db.Queryable<ProjectManagementTaskAttachmentEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && fileIds.Contains(item.FileId) && !attachmentIds.Contains(item.Id))
            .Select(item => item.FileId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var rows = fileIds
            .Except(referencedFileIds, StringComparer.Ordinal)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .Select(fileId => new ProjectManagementPurgeFileDeletionEntity
            {
                TenantId = Tenant(),
                AppCode = App(),
                OperationId = operationId,
                FileId = fileId!,
                Status = "Pending",
                CreatedBy = User(),
                CreatedTime = DateTime.UtcNow
            })
            .ToList();
        if (rows.Count > 0) await db.Insertable(rows).ExecuteCommandAsync(cancellationToken);
    }

    public async Task ScheduleOrphanAsync(ISqlSugarClient db, string operationId, string fileId, CancellationToken cancellationToken = default)
    {
        var normalizedFileId = fileId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedFileId)) return;
        if (await db.Queryable<ProjectManagementTaskAttachmentEntity>()
            .AnyAsync(item => item.TenantId == Tenant() && item.AppCode == App() && item.FileId == normalizedFileId, cancellationToken)) return;

        await db.Insertable(new ProjectManagementPurgeFileDeletionEntity
        {
            TenantId = Tenant(),
            AppCode = App(),
            OperationId = operationId,
            FileId = normalizedFileId,
            Status = "Pending",
            CreatedBy = User(),
            CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<bool> TryProcessAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetCurrentDb();
        var rows = await db.Queryable<ProjectManagementPurgeFileDeletionEntity>()
            .Where(item => item.OperationId == operationId && item.TenantId == Tenant() && item.AppCode == App() && item.Status != "Completed")
            .OrderBy(item => item.CreatedTime)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return false;
        foreach (var row in rows)
        {
            try
            {
                await fileStore.DeleteAsync(row.FileId, cancellationToken);
                row.Status = "Completed";
                row.CompletedTime = DateTime.UtcNow;
                row.LastError = null;
            }
            catch (Exception exception) when (IsMissingFile(exception))
            {
                row.Status = "Completed";
                row.CompletedTime = DateTime.UtcNow;
                row.LastError = "文件已不存在，按幂等清理完成";
            }
            catch (Exception exception)
            {
                row.Status = "Pending";
                row.LastError = exception.Message.Length > 1000 ? exception.Message[..1000] : exception.Message;
                row.AttemptCount++;
                row.UpdatedBy = User();
                row.UpdatedTime = DateTime.UtcNow;
                await db.Updateable(row).ExecuteCommandAsync(CancellationToken.None);
                throw;
            }
            row.AttemptCount++;
            row.UpdatedBy = User();
            row.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(row).ExecuteCommandAsync(cancellationToken);
        }
        return true;
    }

    private static bool IsMissingFile(Exception exception) => exception switch
    {
        NotFoundException { Code: ErrorCodes.FileNotFound } => true,
        FileNotFoundException or DirectoryNotFoundException => true,
        _ when exception.InnerException is not null => IsMissingFile(exception.InnerException),
        _ => false
    };

    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new InvalidOperationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new InvalidOperationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new InvalidOperationException("当前会话缺少用户");
}
