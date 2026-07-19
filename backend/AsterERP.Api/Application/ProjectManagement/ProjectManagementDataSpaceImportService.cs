using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Files;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 只恢复由本平台受控导出服务签发的加密包。导入采用逻辑替换，永远不直接替换正在使用的 SQLite 文件。
/// </summary>
public sealed class ProjectManagementDataSpaceImportService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementRiskConfirmationService riskConfirmation,
    IProjectManagementMaintenanceLock maintenanceLock,
    IProjectManagementOperationWriter operationWriter,
    IBackgroundJobManager backgroundJobManager,
    IFileStorageService fileStorageService,
    IDataProtectionProvider dataProtectionProvider,
    IHostEnvironment environment) : IProjectManagementDataSpaceImportService
{
    private const int SupportedFormatVersion = 1;
    private readonly IDataProtector packageKeyProtector = dataProtectionProvider.CreateProtector("AsterERP.ProjectManagement.DataSpaceExport.PackageKey.v1");

    public async Task<ProjectManagementDataSpaceImportResponse> StartAsync(ProjectManagementDataSpaceImportRequest request, CancellationToken cancellationToken = default)
    {
        RequirePlatformImportPermission();
        await riskConfirmation.EnsureConfirmedAsync(request.CurrentPassword, request.ConfirmRisk, cancellationToken);
        var export = await GetReadyExportAsync(Required(request.ExportId, "导出包标识不能为空"), cancellationToken);
        var manifest = DeserializeManifest(export.ManifestJson) ?? throw new ValidationException("导出包缺少清单，不能导入");
        VerifyManifest(manifest);
        var operationId = Guid.NewGuid().ToString("N");
        var impact = new ImportImpact(export.Id, export.PackageName, NormalizeReason(request.Reason), "Pending", ActivityTraceId(operationId));
        await operationWriter.CreatePendingAsync(operationId, "data-space.database-import", JsonSerializer.Serialize(impact), impact.TraceId, cancellationToken);
        try
        {
            await backgroundJobManager.EnqueueAsync(new ProjectManagementOperationJobArgs(operationId, Tenant(), App(), UserId(), impact.TraceId));
        }
        catch (Exception exception)
        {
            await operationWriter.FailAsync(operationId, $"整库导入入队失败：{exception.Message}", CancellationToken.None);
            throw;
        }
        return new ProjectManagementDataSpaceImportResponse(operationId, export.Id, "Pending", DateTime.UtcNow);
    }

    public async Task ExecuteAsync(string operationId, CancellationToken cancellationToken = default)
    {
        RequirePlatformImportPermission();
        var impact = await GetImpactAsync(operationId, cancellationToken);
        var export = await GetReadyExportAsync(impact.ExportId, cancellationToken);
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "astererp-data-space-imports", operationId);
        Directory.CreateDirectory(temporaryRoot);
        var packagePath = Path.Combine(temporaryRoot, export.PackageName);
        var importedDbPath = Path.Combine(temporaryRoot, "import.sqlite");
        string? safetyPath = null;
        string? lockId = null;
        try
        {
            await operationWriter.StartAsync(operationId, "data-space.database-import", JsonSerializer.Serialize(impact with { Status = "Running" }), impact.TraceId, cancellationToken);
            await RequireProgressAsync(operationId, "正在校验受控导入包", 10, cancellationToken);
            await DownloadAndVerifyPackageAsync(export, packagePath, cancellationToken);
            var manifest = await DecryptAndValidatePackageAsync(export, packagePath, importedDbPath, cancellationToken);
            VerifyManifest(manifest);

            // 在接触在线库前完成所有不变量验证；随后整个替换期使用维护锁防止并发写入。
            lockId = await maintenanceLock.AcquireAsync("project-management-data-space-import", TimeSpan.FromMinutes(30), cancellationToken);
            await RequireProgressAsync(operationId, "正在创建导入前安全备份", 30, cancellationToken);
            safetyPath = await CreateSafetySnapshotAsync(operationId, cancellationToken);
            await RequireProgressAsync(operationId, "正在执行事务数据替换", 55, cancellationToken);
            await MigrateImportedSnapshotAsync(importedDbPath, cancellationToken);
            await ReplaceWorkspaceRowsAsync(importedDbPath, cancellationToken);
            await RequireProgressAsync(operationId, "正在校验并重建运行状态", 80, cancellationToken);
            await EnsureIntegrityAsync(cancellationToken);
            SqliteConnection.ClearAllPools();
            await operationWriter.FailRunningExceptAsync(operationId, "整库导入已替换项目管理数据，原会话、索引和排队作业必须重新建立", cancellationToken);
            await RequireProgressAsync(operationId, "已完成会话、缓存、索引与作业重建通知", 95, cancellationToken);
            await operationWriter.CompleteWithImpactAsync(operationId, JsonSerializer.Serialize(impact with { Status = "Succeeded" }), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await operationWriter.CancelAsync(operationId, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            var rollbackSucceeded = false;
            if (safetyPath is not null)
            {
                try
                {
                    await ReplaceWorkspaceRowsAsync(safetyPath, CancellationToken.None);
                    await EnsureIntegrityAsync(CancellationToken.None);
                    rollbackSucceeded = true;
                }
                catch (Exception rollbackException)
                {
                    await operationWriter.FailAsync(operationId, $"整库导入失败且自动回滚失败：{rollbackException.Message}", CancellationToken.None);
                    throw new ValidationException($"整库导入失败且自动回滚失败：{rollbackException.Message}");
                }
            }
            await operationWriter.FailAsync(operationId, rollbackSucceeded
                ? $"整库导入失败，已自动恢复导入前安全备份：{exception.Message}"
                : $"整库导入失败（尚未替换数据）：{exception.Message}", CancellationToken.None);
            throw;
        }
        finally
        {
            if (lockId is not null) await maintenanceLock.ReleaseAsync(lockId, CancellationToken.None);
            TryDeleteDirectory(temporaryRoot);
        }
    }

    private async Task DownloadAndVerifyPackageAsync(ProjectManagementDataSpaceExportEntity export, string packagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(export.StoragePath) || string.IsNullOrWhiteSpace(export.PackageSha256))
            throw new ValidationException("导出记录缺少受控包存储或摘要，不能导入");
        await using var source = await fileStorageService.OpenReadAsync(export.StoragePath, cancellationToken);
        await using (var target = File.Create(packagePath)) await source.CopyToAsync(target, cancellationToken);
        var actual = await ComputeSha256Async(packagePath, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(export.PackageSha256), Convert.FromHexString(actual)))
            throw new ValidationException("导入包校验失败，文件可能已被篡改");
    }

    private async Task<ProjectManagementDataSpaceExportManifest> DecryptAndValidatePackageAsync(ProjectManagementDataSpaceExportEntity export, string packagePath, string importedDbPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(export.EncryptionKeyCipherText)) throw new ValidationException("导出包没有可用的受保护密钥封套");
        var envelope = packageKeyProtector.Unprotect(export.EncryptionKeyCipherText).Split(':');
        if (envelope.Length != 2) throw new ValidationException("导出包密钥封套格式无效");
        await using var packageStream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);
        var manifestEntry = archive.GetEntry("manifest.json") ?? throw new ValidationException("导入包缺少 manifest.json");
        ProjectManagementDataSpaceExportManifest? manifest;
        await using (var manifestStream = manifestEntry.Open()) manifest = await JsonSerializer.DeserializeAsync<ProjectManagementDataSpaceExportManifest>(manifestStream, cancellationToken: cancellationToken);
        if (manifest is null) throw new ValidationException("导入包清单无效");
        var payload = archive.GetEntry("database.sqlite.aes") ?? throw new ValidationException("导入包缺少数据库载荷");
        await using (var encrypted = payload.Open())
        await using (var target = File.Create(importedDbPath))
        using (var aes = Aes.Create())
        {
            aes.Key = Convert.FromBase64String(envelope[0]); aes.IV = Convert.FromBase64String(envelope[1]); aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            await using var decrypted = new CryptoStream(encrypted, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: false);
            await decrypted.CopyToAsync(target, cancellationToken);
        }
        var payloadHash = await ComputeSha256Async(importedDbPath, cancellationToken);
        if (!string.Equals(payloadHash, manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase) || !string.Equals(payloadHash, export.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("导入包数据库摘要校验失败");
        return manifest;
    }

    private async Task MigrateImportedSnapshotAsync(string importedDbPath, CancellationToken cancellationToken)
    {
        using var importedDb = new SqlSugarClient(new ConnectionConfig { ConnectionString = new SqliteConnectionStringBuilder { DataSource = importedDbPath }.ToString(), DbType = DbType.Sqlite, IsAutoCloseConnection = true });
        await new ProjectManagementSchemaMigrator().MigrateAsync(importedDb, cancellationToken);
    }

    private async Task<string> CreateSafetySnapshotAsync(string operationId, CancellationToken cancellationToken)
    {
        var source = RequireSqliteConnection();
        var directory = Path.Combine(environment.ContentRootPath, "data", "project-management-import-safety", Tenant(), App());
        Directory.CreateDirectory(directory);
        var safetyPath = Path.Combine(directory, $"{operationId}.db");
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = safetyPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        return safetyPath;
    }

    private async Task ReplaceWorkspaceRowsAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var target = RequireSqliteConnection();
        var alias = $"pm_import_{Guid.NewGuid():N}";
        await using var attach = target.CreateCommand();
        attach.CommandText = $"ATTACH DATABASE $path AS {alias};"; attach.Parameters.AddWithValue("$path", sourcePath);
        await attach.ExecuteNonQueryAsync(cancellationToken);
        try
        {
            await using var transaction = (SqliteTransaction)await target.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var table in WorkspaceTables) await EnsureTableAsync(target, alias, table, cancellationToken);
                foreach (var table in WorkspaceTables) await ExecuteScopedAsync(target, transaction, $"DELETE FROM {Q(table)} WHERE TenantId = $tenantId AND AppCode = $appCode;", cancellationToken);
                foreach (var table in WorkspaceTables) await ExecuteScopedAsync(target, transaction, $"INSERT INTO {Q(table)} SELECT * FROM {alias}.{Q(table)} WHERE TenantId = $tenantId AND AppCode = $appCode;", cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
        }
        finally
        {
            await using var detach = target.CreateCommand(); detach.CommandText = $"DETACH DATABASE {alias};"; await detach.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private async Task EnsureTableAsync(SqliteConnection target, string alias, string table, CancellationToken cancellationToken)
    {
        await using var command = target.CreateCommand(); command.CommandText = $"SELECT COUNT(1) FROM {alias}.sqlite_master WHERE type = 'table' AND name = $table;"; command.Parameters.AddWithValue("$table", table);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1) throw new ValidationException($"导入包缺少项目管理表 {table}");
    }

    private async Task ExecuteScopedAsync(SqliteConnection target, SqliteTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = target.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; command.Parameters.AddWithValue("$tenantId", Tenant()); command.Parameters.AddWithValue("$appCode", App()); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureIntegrityAsync(CancellationToken cancellationToken)
    {
        var result = RequireSqliteConnection().CreateCommand();
        await using (result) { result.CommandText = "PRAGMA integrity_check;"; if (!string.Equals(Convert.ToString(await result.ExecuteScalarAsync(cancellationToken)), "ok", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("导入后的数据库完整性检查失败"); }
    }

    private async Task RequireProgressAsync(string operationId, string phase, int progress, CancellationToken cancellationToken)
    {
        if (!await operationWriter.ReportProgressAsync(operationId, phase, progress, cancellationToken)) throw new OperationCanceledException("整库导入已取消", cancellationToken);
    }

    private async Task<ProjectManagementDataSpaceExportEntity> GetReadyExportAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementDataSpaceExportEntity>().Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && item.Status == "Ready" && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("受控整库导出包不存在或不可导入", ErrorCodes.PlatformResourceNotFound);

    private async Task<ImportImpact> GetImpactAsync(string operationId, CancellationToken cancellationToken)
    {
        var json = (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == operationId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).Select(item => item.ImpactJson).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        return JsonSerializer.Deserialize<ImportImpact>(json ?? string.Empty) ?? throw new ValidationException("整库导入任务上下文无效");
    }

    private void VerifyManifest(ProjectManagementDataSpaceExportManifest manifest)
    {
        if (manifest.FormatVersion != SupportedFormatVersion || !string.Equals(manifest.DatabaseProvider, "SQLite", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("导入包格式或数据库提供者不受支持");
        if (!string.Equals(manifest.TenantId, Tenant(), StringComparison.Ordinal) || !string.Equals(manifest.AppCode, App(), StringComparison.OrdinalIgnoreCase)) throw new ValidationException("导入包工作区与当前租户或应用不匹配");
        if (manifest.SchemaVersion > 5) throw new ValidationException("导入包的项目管理架构版本高于当前平台，不能降级导入");
        if (string.IsNullOrWhiteSpace(manifest.DatabaseSha256)) throw new ValidationException("导入包缺少数据库摘要");
    }

    private SqliteConnection RequireSqliteConnection()
    {
        if (databaseAccessor.GetProjectManagementDb().Ado.Connection is not SqliteConnection connection || string.IsNullOrWhiteSpace(connection.DataSource) || connection.DataSource.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("当前数据空间不支持受控整库导入");
        return connection;
    }

    private void RequirePlatformImportPermission()
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        if (!currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementDataSpaceImport)) throw new ValidationException("没有项目数据空间整库导入权限", ErrorCodes.PermissionDenied);
    }

    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? NormalizeReason(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(200, value.Trim().Length)];
    private static string ActivityTraceId(string fallback) => global::System.Diagnostics.Activity.Current?.Id ?? fallback;
    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken) { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)); }
    private static ProjectManagementDataSpaceExportManifest? DeserializeManifest(string json) { try { return JsonSerializer.Deserialize<ProjectManagementDataSpaceExportManifest>(json); } catch (JsonException) { return null; } }
    private static string Q(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
    private sealed record ImportImpact(string ExportId, string PackageName, string? Reason, string Status, string TraceId);

    private static readonly string[] WorkspaceTables = ["pm_projects", "pm_project_members", "pm_milestones", "pm_tasks", "pm_task_dependencies", "pm_task_labels", "pm_labels", "pm_task_time_logs", "pm_task_templates", "pm_task_occurrences", "pm_task_reminders", "pm_task_participants", "pm_activities", "pm_task_comments", "pm_task_attachments", "pm_sync_journal", "pm_sync_devices", "pm_saved_views", "pm_notifications", "pm_im_conversation_links"];
}
