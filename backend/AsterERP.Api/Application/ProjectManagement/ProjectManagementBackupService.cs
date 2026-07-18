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
        EnsureLogicalBackupSupported();
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
            await ReportProgressAsync(operationId, "正在创建当前项目管理数据快照", 10, cancellationToken);
            var backup = await CreateLogicalBackupAsync(operationId, request.Reason, cancellationToken);
            var db = databaseAccessor.GetProjectManagementDb();
            await db.Insertable(backup).ExecuteCommandAsync(cancellationToken);
            await ReportProgressAsync(operationId, "正在校验备份完整性", 90, cancellationToken);
            if (operationWriter is not null) await operationWriter.SucceedAsync(operationId, cancellationToken);
            return Map(backup, operationId);
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
        EnsureLogicalBackupSupported();
        RequireWorkspace();
        var rows = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementBackupEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Ready")
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(200).ToListAsync(cancellationToken);
        return rows.Select(item => Map(item)).ToList();
    }

    public async Task<ProjectManagementBackupRestorePreviewResponse> PreviewRestoreAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureLogicalBackupSupported();
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
        EnsureLogicalBackupSupported();
        await riskConfirmation.EnsureConfirmedAsync(request.CurrentPassword, request.ConfirmRisk, cancellationToken);
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
            await ReportProgressAsync(operationId, "正在创建恢复前安全快照", 10, cancellationToken);
            safetyPath = await CreateSafetyBackupAsync(operationId, cancellationToken);
            await ReportProgressAsync(operationId, "正在恢复当前项目管理数据空间", 45, cancellationToken);
            await RestoreLogicalDataAsync(backupPath, cancellationToken);
            if (operationWriter is not null)
            {
                await ReportProgressAsync(operationId, "正在清理已失效的项目管理操作", 75, cancellationToken);
                await operationWriter.FailRunningExceptAsync(operationId, "数据库恢复使快照时仍在执行的操作失效", cancellationToken);
            }
            await ReportProgressAsync(operationId, "正在执行数据完整性检查", 90, cancellationToken);
            await EnsureIntegrityAsync(cancellationToken);
            await PersistRestoredMetadataAsync(target, cancellationToken);
            if (operationWriter is not null) await operationWriter.SucceedAsync(operationId, cancellationToken);
            return Map(target, operationId);
        }
        catch (Exception exception)
        {
            if (safetyPath is not null)
            {
                try
                {
                    await RestoreLogicalDataAsync(safetyPath, CancellationToken.None);
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

    private async Task<ProjectManagementBackupEntity> CreateLogicalBackupAsync(string operationId, string? reason, CancellationToken cancellationToken)
    {
        var source = RequireSqliteConnection();
        var backupId = Guid.NewGuid().ToString("N");
        var relativePath = Path.Combine("data", "project-management-backups", Tenant(), App(), $"{backupId}.db");
        var absolutePath = GetAbsoluteBackupPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        try
        {
            await BackupProjectManagementWorkspaceToFileAsync(source, absolutePath, cancellationToken);
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
        await BackupProjectManagementWorkspaceToFileAsync(source, path, cancellationToken);
        return path;
    }

    private async Task RestoreLogicalDataAsync(string backupPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = RequireSqliteConnection();
        var attachmentName = $"pm_restore_{Guid.NewGuid():N}";
        await using var attach = target.CreateCommand();
        attach.CommandText = $"ATTACH DATABASE $backupPath AS {attachmentName};";
        attach.Parameters.AddWithValue("$backupPath", backupPath);
        await attach.ExecuteNonQueryAsync(cancellationToken);
        try
        {
            await using var transaction = (SqliteTransaction)await target.BeginTransactionAsync(cancellationToken);
            try
            {
                await using (var deferConstraints = target.CreateCommand())
                {
                    deferConstraints.Transaction = transaction;
                    deferConstraints.CommandText = "PRAGMA defer_foreign_keys = ON;";
                    await deferConstraints.ExecuteNonQueryAsync(cancellationToken);
                }
                foreach (var table in RestoredTableNames)
                {
                    await EnsureBackupTableAsync(target, attachmentName, table, cancellationToken);
                    await DeleteWorkspaceRowsAsync(target, transaction, table, cancellationToken);
                }

                foreach (var table in RestoredTableNames)
                {
                    await InsertWorkspaceRowsAsync(target, transaction, attachmentName, table, cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            await using var detach = target.CreateCommand();
            detach.CommandText = $"DETACH DATABASE {attachmentName};";
            await detach.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private async Task EnsureIntegrityAsync(CancellationToken cancellationToken)
    {
        var result = databaseAccessor.GetCurrentDb().Ado.GetString("PRAGMA integrity_check;");
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("恢复后数据库完整性检查失败");
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task PersistRestoredMetadataAsync(ProjectManagementBackupEntity backup, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        var existing = await db.Queryable<ProjectManagementBackupEntity>().Where(item => item.Id == backup.Id).Take(1).ToListAsync(cancellationToken);
        if (existing.Count == 0) await db.Insertable(backup).ExecuteCommandAsync(cancellationToken);
        else await db.Updateable(backup).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ProjectManagementBackupEntity> GetReadyBackupAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementBackupEntity>().Where(item => item.Id == id && !item.IsDeleted && item.Status == "Ready").Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new ValidationException("备份不存在或不可恢复");

    private async Task<ProjectManagementDataSpaceImpact> ReadCurrentImpactAsync(CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetProjectManagementDb();
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
        if (databaseAccessor.GetProjectManagementDb().Ado.Connection is not SqliteConnection connection) throw new ValidationException("项目管理平台数据空间不是 SQLite，暂不支持逻辑备份恢复");
        if (string.IsNullOrWhiteSpace(connection.DataSource) || connection.DataSource.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("内存数据库不支持持久化备份恢复");
        return connection;
    }

    private async Task BackupProjectManagementWorkspaceToFileAsync(SqliteConnection source, string path, CancellationToken cancellationToken)
    {
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        await PruneToProjectManagementWorkspaceAsync(destination, cancellationToken);
        await destination.CloseAsync();
        SqliteConnection.ClearPool(destination);
    }

    private async Task PruneToProjectManagementWorkspaceAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var tables = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) tables.Add(reader.GetString(0));
        }

        foreach (var table in tables)
        {
            if (!RestoredTableNames.Contains(table, StringComparer.Ordinal))
            {
                await using var drop = connection.CreateCommand();
                drop.CommandText = $"DROP TABLE {QuoteIdentifier(table)};";
                await drop.ExecuteNonQueryAsync(cancellationToken);
                continue;
            }

            await using var delete = connection.CreateCommand();
            delete.CommandText = $"DELETE FROM {QuoteIdentifier(table)} WHERE {QuoteIdentifier("TenantId")} <> $tenantId OR {QuoteIdentifier("AppCode")} <> $appCode;";
            delete.Parameters.AddWithValue("$tenantId", Tenant());
            delete.Parameters.AddWithValue("$appCode", App());
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task EnsureBackupTableAsync(SqliteConnection target, string attachmentName, string table, CancellationToken cancellationToken)
    {
        await using var command = target.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM {attachmentName}.sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new ValidationException($"备份缺少项目管理数据表 {table}，不能恢复");
    }

    private async Task DeleteWorkspaceRowsAsync(SqliteConnection target, SqliteTransaction transaction, string table, CancellationToken cancellationToken)
    {
        await using var command = target.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {QuoteIdentifier(table)} WHERE {QuoteIdentifier("TenantId")} = $tenantId AND {QuoteIdentifier("AppCode")} = $appCode;";
        command.Parameters.AddWithValue("$tenantId", Tenant());
        command.Parameters.AddWithValue("$appCode", App());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertWorkspaceRowsAsync(SqliteConnection target, SqliteTransaction transaction, string attachmentName, string table, CancellationToken cancellationToken)
    {
        await using var command = target.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO {QuoteIdentifier(table)} SELECT * FROM {attachmentName}.{QuoteIdentifier(table)} WHERE {QuoteIdentifier("TenantId")} = $tenantId AND {QuoteIdentifier("AppCode")} = $appCode;";
        command.Parameters.AddWithValue("$tenantId", Tenant());
        command.Parameters.AddWithValue("$appCode", App());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ReportProgressAsync(string operationId, string phase, int progressPercent, CancellationToken cancellationToken)
    {
        if (operationWriter is null) return;
        if (!await operationWriter.ReportProgressAsync(operationId, phase, progressPercent, cancellationToken))
            throw new OperationCanceledException("备份或恢复操作已取消", cancellationToken);
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

    private static string QuoteIdentifier(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static readonly string[] RestoredTableNames =
    [
        "pm_projects",
        "pm_project_members",
        "pm_milestones",
        "pm_tasks",
        "pm_task_dependencies",
        "pm_task_labels",
        "pm_labels",
        "pm_task_time_logs",
        "pm_task_templates",
        "pm_task_occurrences",
        "pm_task_reminders",
        "pm_task_participants",
        "pm_activities",
        "pm_task_comments",
        "pm_task_attachments",
        "pm_sync_journal",
        "pm_sync_devices",
        "pm_saved_views",
        "pm_notifications",
        "pm_im_conversation_links"
    ];

    private ProjectManagementBackupResponse Map(ProjectManagementBackupEntity entity, string? operationId = null) => new(entity.Id, entity.BackupName, entity.Sha256, entity.FileSize, entity.Status, entity.CreatedByUserId, entity.CreatedTime, entity.CompletedAt, operationId);
    private void EnsureLogicalBackupSupported()
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
    }
    private void RequireWorkspace() { Tenant(); App(); }
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
