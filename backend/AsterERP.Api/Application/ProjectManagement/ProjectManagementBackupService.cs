using System.Security.Cryptography;
using System.Diagnostics;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using Microsoft.Data.Sqlite;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementBackupService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementRiskConfirmationService riskConfirmation,
    IProjectManagementMaintenanceLock maintenanceLock,
    IHostEnvironment environment,
    IProjectManagementOperationWriter? operationWriter = null) : IProjectManagementBackupService
{
    public async Task<ProjectManagementBackupResponse> CreateAsync(ProjectManagementBackupRequest request, CancellationToken cancellationToken = default)
    {
        await riskConfirmation.EnsureConfirmedAsync(request.CurrentPassword, request.ConfirmRisk, cancellationToken);
        var operationId = await maintenanceLock.AcquireAsync("project-management-backup", TimeSpan.FromMinutes(15), cancellationToken);
        var operationStarted = false;
        try
        {
            if (operationWriter is not null)
            {
                await operationWriter.StartAsync(operationId, "backup.create", "{\"operation\":\"create-backup\"}", Activity.Current?.Id ?? operationId, cancellationToken);
                operationStarted = true;
            }
            var backup = await CreatePhysicalBackupAsync(operationId, request.Reason, cancellationToken);
            var db = databaseAccessor.GetCurrentDb();
            await db.Insertable(backup).ExecuteCommandAsync(cancellationToken);
            if (operationWriter is not null) await operationWriter.SucceedAsync(operationId, cancellationToken);
            return Map(backup);
        }
        catch (Exception exception)
        {
            if (operationStarted && operationWriter is not null)
            {
                try { await operationWriter.FailAsync(operationId, exception.Message, CancellationToken.None); } catch { }
            }
            throw;
        }
        finally
        {
            await maintenanceLock.ReleaseAsync(operationId, CancellationToken.None);
        }
    }

    public async Task<IReadOnlyList<ProjectManagementBackupResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        RequireWorkspace();
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementBackupEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Ready")
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(200).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ProjectManagementBackupRestorePreviewResponse> PreviewRestoreAsync(string id, CancellationToken cancellationToken = default)
    {
        RequireWorkspace();
        var target = await GetReadyBackupAsync(id, cancellationToken);
        var backupPath = GetAbsoluteBackupPath(target.RelativePath);
        await VerifyFileAsync(backupPath, target.Sha256, target.FileSize, cancellationToken);
        await using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await backup.OpenAsync(cancellationToken);
        return new ProjectManagementBackupRestorePreviewResponse(
            Map(target),
            await ReadCurrentImpactAsync(cancellationToken),
            await ReadImpactAsync(backup, cancellationToken),
            $"将覆盖当前租户 {Tenant()}、应用 {App()} 的整个 SQLite 数据空间，而不只是项目管理记录。",
            "恢复失败时服务端会尝试恢复操作前的临时安全快照，并再次执行数据库完整性检查。",
            "恢复成功后不会保留单击撤销点；如需恢复，只能选择其他完整数据空间备份。" );
    }

    public async Task<ProjectManagementBackupResponse> RestoreAsync(string id, ProjectManagementRestoreRequest request, CancellationToken cancellationToken = default)
    {
        await riskConfirmation.EnsureConfirmedAsync(request.CurrentPassword, request.ConfirmRisk, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var target = await GetReadyBackupAsync(id, cancellationToken);
        var backupPath = GetAbsoluteBackupPath(target.RelativePath);
        await VerifyFileAsync(backupPath, target.Sha256, target.FileSize, cancellationToken);
        var operationId = await maintenanceLock.AcquireAsync("project-management-restore", TimeSpan.FromMinutes(30), cancellationToken);
        var operationStarted = false;
        string? safetyPath = null;
        try
        {
            if (operationWriter is not null)
            {
                await operationWriter.StartAsync(operationId, "backup.restore", $"{{\"backupId\":\"{target.Id}\"}}", Activity.Current?.Id ?? operationId, cancellationToken);
                operationStarted = true;
            }
            safetyPath = await CreateSafetyBackupAsync(operationId, cancellationToken);
            await RestoreSqliteAsync(backupPath, cancellationToken);
            if (operationWriter is not null)
            {
                await operationWriter.StartAsync(operationId, "backup.restore", $"{{\"backupId\":\"{target.Id}\"}}", Activity.Current?.Id ?? operationId, cancellationToken);
                await operationWriter.FailRunningExceptAsync(operationId, "数据库恢复使快照时仍在执行的操作失效", cancellationToken);
            }
            await EnsureIntegrityAsync(cancellationToken);
            await PersistRestoredMetadataAsync(target, cancellationToken);
            if (operationWriter is not null) await operationWriter.SucceedAsync(operationId, cancellationToken);
            return Map(target);
        }
        catch (Exception exception)
        {
            if (safetyPath is not null)
            {
                try
                {
                    await RestoreSqliteAsync(safetyPath, CancellationToken.None);
                    await EnsureIntegrityAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    throw new ValidationException($"恢复失败且自动回滚失败：{rollbackException.Message}");
                }
            }
            if (operationStarted && operationWriter is not null)
            {
                try { await operationWriter.FailAsync(operationId, exception.Message, CancellationToken.None); } catch { }
            }
            throw;
        }
        finally
        {
            await maintenanceLock.ReleaseAsync(operationId, CancellationToken.None);
            if (safetyPath is not null) TryDelete(safetyPath);
        }
    }

    private async Task<ProjectManagementBackupEntity> CreatePhysicalBackupAsync(string operationId, string? reason, CancellationToken cancellationToken)
    {
        var source = RequireSqliteConnection();
        var backupId = Guid.NewGuid().ToString("N");
        var relativePath = Path.Combine("data", "project-management-backups", Tenant(), App(), $"{backupId}.db");
        var absolutePath = GetAbsoluteBackupPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        try
        {
            await BackupToFileAsync(source, absolutePath, cancellationToken);
            var info = new FileInfo(absolutePath);
            var sha256 = await ComputeSha256Async(absolutePath, cancellationToken);
            return new ProjectManagementBackupEntity
            {
                Id = backupId,
                TenantId = Tenant(), AppCode = App(),
                BackupName = string.IsNullOrWhiteSpace(reason) ? $"自动备份-{DateTime.UtcNow:yyyyMMddHHmmss}" : reason.Trim(),
                RelativePath = relativePath.Replace('\\', '/'), Sha256 = sha256, FileSize = info.Length,
                Status = "Ready", CreatedByUserId = UserId(), CreatedBy = UserId(), CreatedTime = DateTime.UtcNow, CompletedAt = DateTime.UtcNow,
                Remark = operationId
            };
        }
        catch
        {
            TryDelete(absolutePath);
            throw;
        }
    }

    private async Task<string> CreateSafetyBackupAsync(string operationId, CancellationToken cancellationToken)
    {
        var source = RequireSqliteConnection();
        var path = Path.Combine(environment.ContentRootPath, "data", "project-management-backups", Tenant(), App(), $"safety-{operationId}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await BackupToFileAsync(source, path, cancellationToken);
        return path;
    }

    private async Task RestoreSqliteAsync(string backupPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = RequireSqliteConnection();
        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await source.OpenAsync(cancellationToken);
        source.BackupDatabase(target);
    }

    private async Task EnsureIntegrityAsync(CancellationToken cancellationToken)
    {
        var result = databaseAccessor.GetCurrentDb().Ado.GetString("PRAGMA integrity_check;");
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("恢复后数据库完整性检查失败");
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task PersistRestoredMetadataAsync(ProjectManagementBackupEntity backup, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var existing = await db.Queryable<ProjectManagementBackupEntity>().Where(item => item.Id == backup.Id).Take(1).ToListAsync(cancellationToken);
        if (existing.Count == 0) await db.Insertable(backup).ExecuteCommandAsync(cancellationToken);
        else await db.Updateable(backup).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ProjectManagementBackupEntity> GetReadyBackupAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementBackupEntity>().Where(item => item.Id == id && !item.IsDeleted && item.Status == "Ready").Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new ValidationException("备份不存在或不可恢复");

    private async Task<ProjectManagementDataSpaceImpact> ReadCurrentImpactAsync(CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        return new ProjectManagementDataSpaceImpact(Tenant(), App(),
            await db.Queryable<ProjectManagementProjectEntity>().CountAsync(cancellationToken),
            await db.Queryable<ProjectManagementTaskEntity>().CountAsync(cancellationToken),
            await db.Queryable<ProjectManagementProjectMemberEntity>().CountAsync(cancellationToken),
            await db.Queryable<ProjectManagementMilestoneEntity>().CountAsync(cancellationToken),
            await db.Queryable<ProjectManagementTaskAttachmentEntity>().CountAsync(cancellationToken));
    }

    private async Task<ProjectManagementDataSpaceImpact> ReadImpactAsync(SqliteConnection connection, CancellationToken cancellationToken) => new(Tenant(), App(),
        await CountAsync(connection, "pm_projects", cancellationToken), await CountAsync(connection, "pm_tasks", cancellationToken),
        await CountAsync(connection, "pm_project_members", cancellationToken), await CountAsync(connection, "pm_milestones", cancellationToken), await CountAsync(connection, "pm_task_attachments", cancellationToken));

    private static async Task<int> CountAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM {table};";
        try { return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)); }
        catch (SqliteException) { return 0; }
    }

    private SqliteConnection RequireSqliteConnection()
    {
        RequireWorkspace();
        if (databaseAccessor.GetCurrentDb().Ado.Connection is not SqliteConnection connection) throw new ValidationException("当前数据空间不是 SQLite，暂不支持文件级备份恢复");
        if (string.IsNullOrWhiteSpace(connection.DataSource) || connection.DataSource.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("内存数据库不支持持久化备份恢复");
        return connection;
    }

    private async Task BackupToFileAsync(SqliteConnection source, string path, CancellationToken cancellationToken)
    {
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        await destination.CloseAsync();
        SqliteConnection.ClearPool(destination);
    }

    private string GetAbsoluteBackupPath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(environment.ContentRootPath);
        var absolute = Path.GetFullPath(Path.Combine(root, normalized));
        var allowed = Path.GetFullPath(Path.Combine(root, "data", "project-management-backups", Tenant(), App()));
        var relative = Path.GetRelativePath(allowed, absolute);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new ValidationException("备份路径不合法");
        return absolute;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static async Task VerifyFileAsync(string path, string expectedSha256, long expectedSize, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != expectedSize || !string.Equals(await ComputeSha256Async(path, cancellationToken), expectedSha256, StringComparison.OrdinalIgnoreCase)) throw new ValidationException("备份文件校验失败");
    }

    private ProjectManagementBackupResponse Map(ProjectManagementBackupEntity entity) => new(entity.Id, entity.BackupName, entity.Sha256, entity.FileSize, entity.Status, entity.CreatedByUserId, entity.CreatedTime, entity.CompletedAt);
    private void RequireWorkspace() { Tenant(); App(); }
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
