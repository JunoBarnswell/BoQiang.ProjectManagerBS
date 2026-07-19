using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
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
/// 受控整库导出：SQLite 在线备份仅在后台任务中短暂读取源库，产物以 AES 加密包保存到平台 Blob 存储。
/// </summary>
public sealed class ProjectManagementDataSpaceExportService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementRiskConfirmationService riskConfirmation,
    IProjectManagementOperationWriter operationWriter,
    IBackgroundJobManager backgroundJobManager,
    IFileStorageService fileStorageService,
    IDataProtectionProvider dataProtectionProvider) : IProjectManagementDataSpaceExportService
{
    private const int PackageFormatVersion = 1;
    private const int DefaultRetentionHours = 24;
    private const int DefaultMaxDownloadCount = 3;
    private readonly IDataProtector packageKeyProtector = dataProtectionProvider.CreateProtector("AsterERP.ProjectManagement.DataSpaceExport.PackageKey.v1");

    public async Task<ProjectManagementDataSpaceExportResponse> StartAsync(ProjectManagementDataSpaceExportRequest request, CancellationToken cancellationToken = default)
    {
        RequirePlatformExportPermission();
        await riskConfirmation.EnsureConfirmedAsync(request.CurrentPassword, request.ConfirmRisk, cancellationToken);
        var operationId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var expiresAt = now.AddHours(DefaultRetentionHours);
        var export = new ProjectManagementDataSpaceExportEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = Tenant(),
            AppCode = App(),
            OperationId = operationId,
            PackageName = $"project-management-database-{Tenant()}-{App()}-{now:yyyyMMddHHmmss}.bqdbx",
            Status = "Pending",
            CreatedByUserId = UserId(),
            DownloadExpiresAt = expiresAt,
            MaxDownloadCount = DefaultMaxDownloadCount,
            CreatedBy = UserId(),
            CreatedTime = now,
            Remark = NormalizeReason(request.Reason)
        };
        var impactJson = JsonSerializer.Serialize(new ExportOperationImpact(export.Id, export.PackageName, export.DownloadExpiresAt, export.MaxDownloadCount));
        await databaseAccessor.GetProjectManagementDb().Insertable(export).ExecuteCommandAsync(cancellationToken);
        try
        {
            await operationWriter.CreatePendingAsync(operationId, "data-space.database-export", impactJson, ActivityTraceId(operationId), cancellationToken);
            await backgroundJobManager.EnqueueAsync(new ProjectManagementOperationJobArgs(operationId, Tenant(), App(), UserId(), ActivityTraceId(operationId)));
        }
        catch (Exception exception)
        {
            export.Status = "Failed";
            export.UpdatedBy = UserId();
            export.UpdatedTime = DateTime.UtcNow;
            await databaseAccessor.GetProjectManagementDb().Updateable(export).ExecuteCommandAsync(CancellationToken.None);
            try { await operationWriter.FailAsync(operationId, $"整库导出入队失败：{exception.Message}", CancellationToken.None); } catch { }
            throw;
        }

        return Map(export, null);
    }

    public async Task<IReadOnlyList<ProjectManagementDataSpaceExportResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        RequirePlatformExportPermission();
        var rows = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementDataSpaceExportEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(100)
            .ToListAsync(cancellationToken);
        return rows.Select(item => Map(item, DeserializeManifest(item.ManifestJson))).ToList();
    }

    public async Task ExecuteAsync(string operationId, CancellationToken cancellationToken = default)
    {
        RequirePlatformExportPermission();
        var export = await GetOwnedByOperationAsync(operationId, cancellationToken);
        if (export.Status is "Ready" or "Expired") return;
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "astererp-data-space-exports", export.Id);
        Directory.CreateDirectory(temporaryRoot);
        var snapshotPath = Path.Combine(temporaryRoot, "snapshot.sqlite");
        var packagePath = Path.Combine(temporaryRoot, export.PackageName);
        try
        {
            await operationWriter.StartAsync(operationId, "data-space.database-export", await ReadImpactJsonAsync(operationId, cancellationToken), ActivityTraceId(operationId), cancellationToken);
            if (!await operationWriter.ReportProgressAsync(operationId, "正在创建在线一致性快照", 10, cancellationToken)) return;
            await CreateOnlineSnapshotAsync(snapshotPath, cancellationToken);
            if (!await operationWriter.ReportProgressAsync(operationId, "正在读取架构与校验数据", 45, cancellationToken)) return;
            var databaseSha256 = await ComputeSha256Async(snapshotPath, cancellationToken);
            var manifest = await BuildManifestAsync(snapshotPath, databaseSha256, cancellationToken);
            if (!await operationWriter.ReportProgressAsync(operationId, "正在加密并封装导出包", 65, cancellationToken)) return;
            var protectedKey = await CreateEncryptedPackageAsync(snapshotPath, packagePath, manifest, cancellationToken);
            var packageSha256 = await ComputeSha256Async(packagePath, cancellationToken);
            var packageSize = new FileInfo(packagePath).Length;
            if (await operationWriter.IsCancellationRequestedAsync(operationId, cancellationToken))
            {
                await operationWriter.CancelAsync(operationId, cancellationToken);
                return;
            }

            await using var packageStream = File.OpenRead(packagePath);
            var storagePath = await fileStorageService.SaveAsync(packageStream, export.PackageName, cancellationToken);
            export.Status = "Ready";
            export.StoragePath = storagePath;
            export.PackageSha256 = packageSha256;
            export.PackageSize = packageSize;
            export.DatabaseSha256 = databaseSha256;
            export.ManifestJson = JsonSerializer.Serialize(manifest);
            export.EncryptionKeyCipherText = protectedKey;
            export.CompletedAt = DateTime.UtcNow;
            export.UpdatedBy = UserId();
            export.UpdatedTime = export.CompletedAt;
            await databaseAccessor.GetProjectManagementDb().Updateable(export).ExecuteCommandAsync(cancellationToken);
            await operationWriter.CompleteWithImpactAsync(operationId, JsonSerializer.Serialize(new ExportOperationImpact(export.Id, export.PackageName, export.DownloadExpiresAt, export.MaxDownloadCount, export.PackageSha256, export.DatabaseSha256)), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await operationWriter.CancelAsync(operationId, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            export.Status = "Failed";
            export.UpdatedBy = UserId();
            export.UpdatedTime = DateTime.UtcNow;
            try { await databaseAccessor.GetProjectManagementDb().Updateable(export).ExecuteCommandAsync(CancellationToken.None); } catch { }
            try { await operationWriter.FailAsync(operationId, $"整库导出失败：{exception.Message}", CancellationToken.None); } catch { }
        }
        finally
        {
            TryDeleteDirectory(temporaryRoot);
        }
    }

    public async Task<ProjectManagementDataSpaceExportDownload> DownloadAsync(string id, CancellationToken cancellationToken = default)
    {
        RequirePlatformExportPermission();
        var export = await GetOwnedAsync(id, cancellationToken);
        var now = DateTime.UtcNow;
        if (!string.Equals(export.Status, "Ready", StringComparison.Ordinal) || now >= export.DownloadExpiresAt)
        {
            if (string.Equals(export.Status, "Ready", StringComparison.Ordinal) && now >= export.DownloadExpiresAt)
            {
                export.Status = "Expired";
                export.UpdatedBy = UserId();
                export.UpdatedTime = now;
                await databaseAccessor.GetProjectManagementDb().Updateable(export).ExecuteCommandAsync(cancellationToken);
            }
            throw new ValidationException("导出包不可下载或已超过有效期");
        }

        var stream = await fileStorageService.OpenReadAsync(export.StoragePath, cancellationToken);
        var affected = await databaseAccessor.GetProjectManagementDb().Updateable<ProjectManagementDataSpaceExportEntity>()
            .SetColumns(item => new ProjectManagementDataSpaceExportEntity { DownloadCount = item.DownloadCount + 1, LastDownloadedAt = now, UpdatedBy = UserId(), UpdatedTime = now })
            .Where(item => item.Id == export.Id && item.TenantId == Tenant() && item.AppCode == App() && item.Status == "Ready" && item.DownloadExpiresAt > now && item.DownloadCount < item.MaxDownloadCount && !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);
        if (affected != 1)
        {
            await stream.DisposeAsync();
            throw new ValidationException("导出包下载次数已达到上限或状态已变化");
        }

        return new ProjectManagementDataSpaceExportDownload(export.PackageName, "application/vnd.astererp.data-space-export", stream);
    }

    private async Task CreateOnlineSnapshotAsync(string snapshotPath, CancellationToken cancellationToken)
    {
        var source = RequireSqliteConnection();
        var openedHere = source.State != global::System.Data.ConnectionState.Open;
        if (openedHere) await source.OpenAsync(cancellationToken);
        try
        {
            await using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshotPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString()))
            {
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
                await destination.CloseAsync();
                SqliteConnection.ClearPool(destination);
            }
        }
        finally
        {
            if (openedHere) await source.CloseAsync();
        }
    }

    private async Task<ProjectManagementDataSpaceExportManifest> BuildManifestAsync(string snapshotPath, string databaseSha256, CancellationToken cancellationToken)
    {
        await using var snapshot = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshotPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await snapshot.OpenAsync(cancellationToken);
        var objects = new List<string>();
        await using (var command = snapshot.CreateCommand())
        {
            command.CommandText = "SELECT type || ':' || name FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type, name;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) objects.Add(reader.GetString(0));
        }
        var schemaVersion = 0;
        await using (var command = snapshot.CreateCommand())
        {
            command.CommandText = "SELECT VersionNo FROM pm_schema_versions WHERE ModuleKey = 'project-management' LIMIT 1;";
            try { schemaVersion = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0); } catch (SqliteException) { }
        }
        return new ProjectManagementDataSpaceExportManifest(PackageFormatVersion, Tenant(), App(), "SQLite", "sqlite-online-backup", schemaVersion, DateTime.UtcNow, objects, databaseSha256, "AES-256-CBC + ASP.NET Core Data Protection key envelope");
    }

    private async Task<string> CreateEncryptedPackageAsync(string snapshotPath, string packagePath, ProjectManagementDataSpaceExportManifest manifest, CancellationToken cancellationToken)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var iv = RandomNumberGenerator.GetBytes(16);
        await using (var package = File.Create(packagePath))
        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: false))
        {
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using (var manifestStream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(manifestStream, manifest, cancellationToken: cancellationToken);
            }

            var payloadEntry = archive.CreateEntry("database.sqlite.aes", CompressionLevel.NoCompression);
            await using var payloadStream = payloadEntry.Open();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            await using var encryptedStream = new CryptoStream(payloadStream, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: false);
            await using var sourceStream = File.OpenRead(snapshotPath);
            await sourceStream.CopyToAsync(encryptedStream, cancellationToken);
            await encryptedStream.FlushFinalBlockAsync(cancellationToken);
        }

        return packageKeyProtector.Protect($"{Convert.ToBase64String(key)}:{Convert.ToBase64String(iv)}");
    }

    private SqliteConnection RequireSqliteConnection()
    {
        if (databaseAccessor.GetProjectManagementDb().Ado.Connection is not SqliteConnection connection)
            throw new ValidationException("当前项目管理数据空间不是 SQLite，无法执行在线整库导出");
        if (string.IsNullOrWhiteSpace(connection.DataSource) || connection.DataSource.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("内存数据库不支持持久化整库导出");
        return connection;
    }

    private async Task<ProjectManagementDataSpaceExportEntity> GetOwnedAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementDataSpaceExportEntity>()
            .Where(item => item.Id == Required(id) && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("整库导出记录不存在", ErrorCodes.PlatformResourceNotFound);

    private async Task<ProjectManagementDataSpaceExportEntity> GetOwnedByOperationAsync(string operationId, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementDataSpaceExportEntity>()
            .Where(item => item.OperationId == Required(operationId) && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("整库导出任务不存在", ErrorCodes.PlatformResourceNotFound);

    private async Task<string> ReadImpactJsonAsync(string operationId, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == operationId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
            .Select(item => item.ImpactJson).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? "{}";

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static ProjectManagementDataSpaceExportManifest? DeserializeManifest(string json)
    {
        try { return string.IsNullOrWhiteSpace(json) || json == "{}" ? null : JsonSerializer.Deserialize<ProjectManagementDataSpaceExportManifest>(json); }
        catch (JsonException) { return null; }
    }

    private static ProjectManagementDataSpaceExportResponse Map(ProjectManagementDataSpaceExportEntity entity, ProjectManagementDataSpaceExportManifest? manifest) =>
        new(entity.Id, entity.PackageName, entity.Status, entity.OperationId, entity.PackageSize, entity.PackageSha256, entity.CreatedTime, entity.CompletedAt, entity.DownloadExpiresAt, entity.DownloadCount, entity.MaxDownloadCount, manifest);

    private void RequirePlatformExportPermission()
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        if (!currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementDataSpaceExport))
            throw new ValidationException("没有项目数据空间整库导出权限", ErrorCodes.PermissionDenied);
    }

    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static string ActivityTraceId(string fallback) => global::System.Diagnostics.Activity.Current?.Id ?? fallback;
    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException("导出标识不能为空") : value.Trim();
    private static string? NormalizeReason(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(200, value.Trim().Length)];
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

    private sealed record ExportOperationImpact(string ExportId, string PackageName, DateTime DownloadExpiresAt, int MaxDownloadCount, string? PackageSha256 = null, string? DatabaseSha256 = null);
}
