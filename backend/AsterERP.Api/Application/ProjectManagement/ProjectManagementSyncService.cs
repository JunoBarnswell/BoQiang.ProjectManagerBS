using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Domain.Common;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementSyncService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy,
    IPasswordHashService passwordHashService,
    IProjectManagementFileStore? fileStore = null,
    IProjectManagementMaintenanceLock? maintenanceLock = null,
    IProjectManagementOperationWriter? operationWriter = null) : IProjectManagementSyncService
{
    private const string Magic = "BQSYNC";
    private const string SchemaVersion = "1";
    private const int MaxPackageBytes = 200 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(byte[] Content, string FileName)> ExportAsync(ProjectManagementSyncExportRequest request, CancellationToken cancellationToken = default)
    {
        var projectIds = await ResolveScopeAsync(request.ProjectId, cancellationToken);
        var snapshot = await LoadSnapshotAsync(projectIds, cancellationToken);
        var journalSequenceNo = await GetJournalSequenceNoAsync(projectIds, cancellationToken);
        var packageId = Guid.NewGuid().ToString("N");
        var data = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        var manifest = new SyncManifest
        {
            Magic = Magic,
            SchemaVersion = SchemaVersion,
            PackageId = packageId,
            TenantId = Tenant(),
            AppCode = App(),
            ExportedAt = DateTime.UtcNow,
            SourceDeviceId = Normalize(request.DeviceId),
            ProjectId = request.ProjectId,
            IncludeAttachments = request.IncludeAttachments,
            DataEntry = "data.json",
            DataSha256 = Sha256(data),
            ProjectCount = snapshot.Projects.Count,
            MemberCount = snapshot.Members.Count,
            MilestoneCount = snapshot.Milestones.Count,
            TaskCount = snapshot.Tasks.Count,
            DependencyCount = snapshot.Dependencies.Count,
            AttachmentCount = snapshot.Attachments.Count,
            JournalSequenceNo = journalSequenceNo
        };

        await using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntryAsync(archive, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions), cancellationToken);
            await WriteEntryAsync(archive, "data.json", data, cancellationToken);
            if (request.IncludeAttachments)
            {
                if (fileStore is null) throw new ValidationException("文件服务不可用，无法导出附件");
                foreach (var attachment in snapshot.Attachments)
                {
                    await using var sourceStream = await fileStore.OpenReadAsync(attachment.FileId, cancellationToken);
                    await using var entryStream = archive.CreateEntry($"attachments/{attachment.FileId}", CompressionLevel.Fastest).Open();
                    await sourceStream.CopyToAsync(entryStream, cancellationToken);
                }
            }
        }

        var content = output.ToArray();
        if (content.Length > MaxPackageBytes) throw new ValidationException("同步包超过 200 MB 限制", ErrorCodes.SchemaOrPayloadTooLarge);
        if (!string.IsNullOrWhiteSpace(request.DeviceId))
            await TouchDeviceAsync(request.DeviceId, journalSequenceNo, cancellationToken);
        return (content, $"project-management-{DateTime.UtcNow:yyyyMMddHHmmss}-{packageId}.bqsync");
    }

    public async Task<ProjectManagementSyncWatermarkResponse> GetWatermarkAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRequiredDevice(deviceId);
        var current = await GetJournalSequenceNoAsync(await ResolveScopeAsync(null, cancellationToken), cancellationToken);
        var device = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementSyncDeviceEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.DeviceId == normalized && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken);
        var row = device.FirstOrDefault();
        return new ProjectManagementSyncWatermarkResponse(normalized, current, row?.LastAcknowledgedSequenceNo ?? 0, row?.LastSeenAt);
    }

    public async Task<IReadOnlyList<ProjectManagementSyncJournalItem>> GetChangesAsync(string? projectId, long sinceSequenceNo, int limit, CancellationToken cancellationToken = default)
    {
        if (sinceSequenceNo < 0) throw new ValidationException("同步起始水位不能为负数");
        var projectIds = await ResolveScopeAsync(projectId, cancellationToken);
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementSyncJournalEntity>()
            .Where(item => item.SequenceNo > sinceSequenceNo && (item.ProjectId == null || projectIds.Contains(item.ProjectId)) && !item.IsDeleted)
            .OrderBy(item => item.SequenceNo, OrderByType.Asc)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(cancellationToken);
        return rows.Select(item => new ProjectManagementSyncJournalItem(item.SequenceNo, item.AggregateType, item.AggregateId, item.ProjectId, item.Operation, item.VersionNo, item.PayloadJson, item.TraceId, item.CreatedTime)).ToList();
    }

    public async Task<ProjectManagementSyncWatermarkResponse> AcknowledgeAsync(ProjectManagementSyncAcknowledgeRequest request, CancellationToken cancellationToken = default)
    {
        var deviceId = NormalizeRequiredDevice(request.DeviceId);
        if (request.SequenceNo < 0) throw new ValidationException("同步水位不能为负数");
        var current = await GetJournalSequenceNoAsync(await ResolveScopeAsync(null, cancellationToken), cancellationToken);
        if (request.SequenceNo > current) throw new ValidationException("不能确认尚未存在的同步水位");
        var db = databaseAccessor.GetCurrentDb();
        var rows = await db.Queryable<ProjectManagementSyncDeviceEntity>().Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.DeviceId == deviceId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        var row = rows.FirstOrDefault() ?? new ProjectManagementSyncDeviceEntity { TenantId = Tenant(), AppCode = App(), DeviceId = deviceId, CreatedBy = UserId(), CreatedTime = DateTime.UtcNow };
        row.LastAcknowledgedSequenceNo = Math.Max(row.LastAcknowledgedSequenceNo, request.SequenceNo);
        row.LastSeenAt = DateTime.UtcNow;
        row.UpdatedBy = UserId();
        row.UpdatedTime = row.LastSeenAt;
        if (rows.Count == 0) await db.Insertable(row).ExecuteCommandAsync(cancellationToken); else await db.Updateable(row).ExecuteCommandAsync(cancellationToken);
        return new ProjectManagementSyncWatermarkResponse(deviceId, current, row.LastAcknowledgedSequenceNo, row.LastSeenAt);
    }

    public async Task<ProjectManagementSyncPreviewResponse> PreviewAsync(Stream packageStream, CancellationToken cancellationToken = default)
    {
        if (packageStream is null || !packageStream.CanRead) throw new ValidationException("同步包不可读");
        await using var memory = new MemoryStream();
        await packageStream.CopyToAsync(memory, cancellationToken);
        if (memory.Length <= 0 || memory.Length > MaxPackageBytes) throw new ValidationException("同步包大小无效", ErrorCodes.SchemaOrPayloadTooLarge);

        var packageBytes = memory.ToArray();
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(new MemoryStream(packageBytes), ZipArchiveMode.Read);
        }
        catch (InvalidDataException exception)
        {
            throw new ValidationException($"同步包压缩结构无效: {exception.Message}");
        }
        using (archive)
        {
        ValidatePaths(archive);
        var manifest = await ReadJsonAsync<SyncManifest>(archive, "manifest.json", cancellationToken);
        if (!string.Equals(manifest.Magic, Magic, StringComparison.Ordinal) || !string.Equals(manifest.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
            throw new ValidationException("同步包格式或版本不兼容");
        if (!string.Equals(manifest.TenantId, Tenant(), StringComparison.Ordinal) || !string.Equals(manifest.AppCode, App(), StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("同步包工作区与当前工作区不匹配", ErrorCodes.PermissionDenied);

        var data = await ReadBytesAsync(archive, manifest.DataEntry, cancellationToken);
        if (!string.Equals(Sha256(data), manifest.DataSha256, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("同步包校验和不匹配");
        var snapshot = JsonSerializer.Deserialize<SyncSnapshot>(data, JsonOptions) ?? throw new ValidationException("同步包数据为空");
        var projectIds = await ResolveScopeAsync(manifest.ProjectId, cancellationToken);
        var conflicts = await DetectConflictsAsync(snapshot, projectIds, cancellationToken);
        var warnings = manifest.IncludeAttachments && snapshot.Attachments.Count > 0
            ? new[] { "附件内容将在导入阶段按 FileId 和校验和校验" }
            : Array.Empty<string>();
        return new ProjectManagementSyncPreviewResponse(
            manifest.PackageId, manifest.SchemaVersion, manifest.TenantId, manifest.AppCode,
            manifest.ExportedAt, manifest.SourceDeviceId, snapshot.Projects.Count, snapshot.Members.Count,
            snapshot.Milestones.Count, snapshot.Tasks.Count, snapshot.Dependencies.Count, snapshot.Attachments.Count,
            packageBytes.LongLength, Sha256(packageBytes), manifest.JournalSequenceNo, true, warnings, conflicts);
        }
    }

    public async Task<ProjectManagementSyncImportResponse> ImportAsync(Stream packageStream, ProjectManagementSyncImportRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.ConfirmRisk) throw new ValidationException("必须确认高风险导入操作");
        var strategy = request.ConflictStrategy.Trim();
        if (strategy is not ("Skip" or "Overwrite" or "Reject")) throw new ValidationException("冲突策略必须是 Skip、Overwrite 或 Reject");
        await VerifyCurrentPasswordAsync(request.CurrentPassword, cancellationToken);
        var (manifest, snapshot, packageBytes) = await ReadValidatedPackageAsync(packageStream, cancellationToken);
        var projectIds = await ResolveScopeAsync(manifest.ProjectId, cancellationToken);
        ValidateSnapshotScope(snapshot, projectIds);
        var conflicts = await DetectConflictsAsync(snapshot, projectIds, cancellationToken);
        if (strategy == "Reject" && conflicts.Count > 0) throw new ValidationException($"同步包存在 {conflicts.Count} 个冲突，已拒绝导入");

        var operationId = maintenanceLock is null ? null : await maintenanceLock.AcquireAsync("project-management-sync-import", TimeSpan.FromMinutes(30), cancellationToken);
        var operationStarted = false;

        var db = databaseAccessor.GetCurrentDb();
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var importedFiles = new List<string>();
        var warnings = new List<string>();
        try
        {
            if (operationId is not null && operationWriter is not null)
            {
                await operationWriter.StartAsync(operationId, "sync.import", $"{{\"packageId\":\"{manifest.PackageId}\",\"strategy\":\"{strategy}\"}}", operationId, cancellationToken);
                operationStarted = true;
            }
            db.Ado.BeginTran();
            inserted += await UpsertAsync(snapshot.Projects, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.Members, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.Milestones, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.Tasks, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.Dependencies, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.Labels, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.TaskLabels, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.Participants, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.TimeLogs, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.Templates, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.Occurrences, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.Activities, strategy, db, cancellationToken, () => skipped++, () => updated++);
            inserted += await UpsertAsync(snapshot.Comments, strategy, db, cancellationToken, () => skipped++, () => updated++);

            var attachmentCount = await ImportAttachmentsAsync(snapshot.Attachments, manifest, packageBytes, strategy, db, importedFiles, cancellationToken, () => skipped++, () => updated++);
            if (!manifest.IncludeAttachments && snapshot.Attachments.Count > 0) warnings.Add("未包含附件内容，附件记录已跳过");
            db.Ado.CommitTran();
            if (operationId is not null && operationWriter is not null) await operationWriter.SucceedAsync(operationId, cancellationToken);
            return new ProjectManagementSyncImportResponse(manifest.PackageId, strategy, inserted, updated, skipped, attachmentCount, warnings);
        }
        catch (Exception exception)
        {
            db.Ado.RollbackTran();
            foreach (var fileId in importedFiles)
            {
                try { if (fileStore is not null) await fileStore.DeleteAsync(fileId, cancellationToken); } catch { }
            }
            if (operationStarted && operationId is not null && operationWriter is not null)
            {
                try { await operationWriter.FailAsync(operationId, exception.Message, CancellationToken.None); } catch { }
            }
            throw;
        }
        finally
        {
            if (operationId is not null && maintenanceLock is not null)
                await maintenanceLock.ReleaseAsync(operationId, CancellationToken.None);
        }
    }

    private async Task<SyncSnapshot> LoadSnapshotAsync(IReadOnlyList<string> projectIds, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var projects = await db.Queryable<ProjectManagementProjectEntity>().Where(item => projectIds.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
        var members = await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => projectIds.Contains(item.ProjectId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => projectIds.Contains(item.ProjectId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => projectIds.Contains(item.ProjectId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var taskIds = tasks.Select(item => item.Id).ToList();
        var dependencies = await db.Queryable<ProjectManagementTaskDependencyEntity>().Where(item => projectIds.Contains(item.ProjectId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var labels = await db.Queryable<ProjectManagementLabelEntity>().Where(item => !item.IsDeleted && (item.ProjectId == null || projectIds.Contains(item.ProjectId))).ToListAsync(cancellationToken);
        var taskLabels = await db.Queryable<ProjectManagementTaskLabelEntity>().Where(item => projectIds.Contains(item.ProjectId) && taskIds.Contains(item.TaskId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var participants = await db.Queryable<ProjectManagementTaskParticipantEntity>().Where(item => projectIds.Contains(item.ProjectId) && taskIds.Contains(item.TaskId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var timeLogs = await db.Queryable<ProjectManagementTaskTimeLogEntity>().Where(item => projectIds.Contains(item.ProjectId) && taskIds.Contains(item.TaskId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var templates = await db.Queryable<ProjectManagementTaskTemplateEntity>().Where(item => !item.IsDeleted && (item.ProjectId == null || projectIds.Contains(item.ProjectId))).ToListAsync(cancellationToken);
        var occurrences = await db.Queryable<ProjectManagementTaskOccurrenceEntity>().Where(item => projectIds.Contains(item.ProjectId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var activities = await db.Queryable<ProjectManagementActivityEntity>().Where(item => projectIds.Contains(item.ProjectId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var comments = await db.Queryable<ProjectManagementTaskCommentEntity>().Where(item => projectIds.Contains(item.ProjectId) && taskIds.Contains(item.TaskId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var attachments = await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => projectIds.Contains(item.ProjectId) && taskIds.Contains(item.TaskId) && !item.IsDeleted).ToListAsync(cancellationToken);
        return new SyncSnapshot
        {
            Projects = projects, Members = members, Milestones = milestones, Tasks = tasks, Dependencies = dependencies,
            Labels = labels, TaskLabels = taskLabels, Participants = participants, TimeLogs = timeLogs,
            Templates = templates, Occurrences = occurrences, Activities = activities, Comments = comments, Attachments = attachments
        };
    }

    private async Task<IReadOnlyList<string>> ResolveScopeAsync(string? projectId, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            var id = projectId.Trim();
            await accessPolicy.EnsureCanViewProjectAsync(id, cancellationToken);
            if (!await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == id && !item.IsDeleted).AnyAsync(cancellationToken))
                throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
            return [id];
        }

        var ids = await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).Select(item => item.Id).ToListAsync(cancellationToken);
        if (ids.Count == 0) throw new ValidationException("当前授权数据空间没有可导出的项目");
        return ids;
    }

    private async Task<long> GetJournalSequenceNoAsync(IReadOnlyList<string> projectIds, CancellationToken cancellationToken)
    {
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementSyncJournalEntity>()
            .Where(item => item.ProjectId == null || projectIds.Contains(item.ProjectId))
            .OrderBy(item => item.SequenceNo, OrderByType.Desc)
            .Select(item => item.SequenceNo)
            .Take(1)
            .ToListAsync(cancellationToken);
        return rows.FirstOrDefault();
    }

    private async Task TouchDeviceAsync(string deviceId, long sequenceNo, CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequiredDevice(deviceId);
        var db = databaseAccessor.GetCurrentDb();
        var rows = await db.Queryable<ProjectManagementSyncDeviceEntity>().Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.DeviceId == normalized && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var row = rows.FirstOrDefault() ?? new ProjectManagementSyncDeviceEntity { TenantId = Tenant(), AppCode = App(), DeviceId = normalized, CreatedBy = UserId(), CreatedTime = now };
        row.LastExportedSequenceNo = Math.Max(row.LastExportedSequenceNo, sequenceNo);
        row.LastSeenAt = now;
        row.UpdatedBy = UserId();
        row.UpdatedTime = now;
        if (rows.Count == 0) await db.Insertable(row).ExecuteCommandAsync(cancellationToken); else await db.Updateable(row).ExecuteCommandAsync(cancellationToken);
    }

    private static string NormalizeRequiredDevice(string value) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException("设备标识不能为空") : value.Trim();
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);

    private async Task<IReadOnlyList<string>> DetectConflictsAsync(SyncSnapshot snapshot, IReadOnlyList<string> projectIds, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var conflicts = new List<string>();
        var projectIdsInPackage = snapshot.Projects.Select(item => item.Id).ToList();
        var existingProjects = await db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => projectIdsInPackage.Contains(item.Id) && projectIds.Contains(item.Id) && !item.IsDeleted)
            .Select(item => new { item.Id, item.VersionNo }).ToListAsync(cancellationToken);
        conflicts.AddRange(existingProjects.Select(item => $"Project:{item.Id}:local-version-{item.VersionNo}"));
        var taskIds = snapshot.Tasks.Select(item => item.Id).ToList();
        var existingTasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => taskIds.Contains(item.Id) && projectIds.Contains(item.ProjectId) && !item.IsDeleted)
            .Select(item => new { item.Id, item.VersionNo }).ToListAsync(cancellationToken);
        conflicts.AddRange(existingTasks.Select(item => $"Task:{item.Id}:local-version-{item.VersionNo}"));
        return conflicts;
    }

    private void ValidateSnapshotScope(SyncSnapshot snapshot, IReadOnlyList<string> projectIds)
    {
        var workspace = new[] {
            snapshot.Projects.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Members.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Milestones.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Tasks.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Dependencies.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Labels.Select(item => (item.TenantId, item.AppCode)),
            snapshot.TaskLabels.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Participants.Select(item => (item.TenantId, item.AppCode)),
            snapshot.TimeLogs.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Templates.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Occurrences.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Activities.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Comments.Select(item => (item.TenantId, item.AppCode)),
            snapshot.Attachments.Select(item => (item.TenantId, item.AppCode))
        }.SelectMany(item => item);
        if (workspace.Any(item => !string.Equals(item.TenantId, Tenant(), StringComparison.Ordinal) || !string.Equals(item.AppCode, App(), StringComparison.OrdinalIgnoreCase)))
            throw new ValidationException("同步包包含其他工作区数据", ErrorCodes.PermissionDenied);
        if (snapshot.Projects.Any(item => !projectIds.Contains(item.Id, StringComparer.Ordinal)))
            throw new ValidationException("同步包包含未授权项目", ErrorCodes.PermissionDenied);
    }

    private async Task<int> UpsertAsync<T>(
        IReadOnlyList<T> records,
        string strategy,
        ISqlSugarClient db,
        CancellationToken cancellationToken,
        Action onSkip,
        Action onUpdate) where T : EntityBase, new()
    {
        var inserted = 0;
        foreach (var record in records)
        {
            var exists = await db.Queryable<T>().Where(item => item.Id == record.Id && !item.IsDeleted).AnyAsync(cancellationToken);
            if (exists)
            {
                if (strategy == "Skip") { onSkip(); continue; }
                await db.Updateable(record).Where(item => item.Id == record.Id).ExecuteCommandAsync(cancellationToken);
                onUpdate();
            }
            else
            {
                await db.Insertable(record).ExecuteCommandAsync(cancellationToken);
                inserted++;
            }
        }
        return inserted;
    }

    private async Task<int> ImportAttachmentsAsync(
        IReadOnlyList<ProjectManagementTaskAttachmentEntity> records,
        SyncManifest manifest,
        byte[] packageBytes,
        string strategy,
        ISqlSugarClient db,
        ICollection<string> importedFiles,
        CancellationToken cancellationToken,
        Action onSkip,
        Action onUpdate)
    {
        if (!manifest.IncludeAttachments || records.Count == 0) return 0;
        if (fileStore is null) throw new ValidationException("文件服务不可用，无法导入附件");
        using var archive = new ZipArchive(new MemoryStream(packageBytes), ZipArchiveMode.Read);
        var imported = 0;
        foreach (var source in records)
        {
            var exists = await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.Id == source.Id && !item.IsDeleted).AnyAsync(cancellationToken);
            if (exists && strategy == "Skip") { onSkip(); continue; }
            var entry = archive.GetEntry($"attachments/{source.FileId}") ?? throw new ValidationException($"同步包缺少附件 {source.FileId}");
            await using var stream = entry.Open();
            await using var attachmentBytes = new MemoryStream();
            await stream.CopyToAsync(attachmentBytes, cancellationToken);
            attachmentBytes.Position = 0;
            var formFile = new FormFile(attachmentBytes, 0, attachmentBytes.Length, "file", source.FileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = source.ContentType
            };
            var uploaded = await fileStore.StoreAsync(formFile, new ProjectManagementFileUploadContext(ProjectManagementFileWritePurpose.SyncImport), cancellationToken);
            importedFiles.Add(uploaded.Id);
            source.FileId = uploaded.Id;
            if (exists)
            {
                await db.Updateable(source).Where(item => item.Id == source.Id).ExecuteCommandAsync(cancellationToken);
                onUpdate();
            }
            else
            {
                await db.Insertable(source).ExecuteCommandAsync(cancellationToken);
            }
            imported++;
        }
        return imported;
    }

    private async Task VerifyCurrentPasswordAsync(string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new ValidationException("高风险导入必须验证当前密码", ErrorCodes.AuthenticationRequired);
        var userId = currentUser.GetAsterErpUserId()?.Trim();
        var user = (await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => item.Id == userId && !item.IsDeleted && item.Status == "Enabled").Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        if (user is null || !passwordHashService.Verify(user.PasswordHash, password).Success)
            throw new ValidationException("当前密码验证失败", ErrorCodes.AuthenticationRequired);
    }

    private async Task<(SyncManifest Manifest, SyncSnapshot Snapshot, byte[] PackageBytes)> ReadValidatedPackageAsync(Stream packageStream, CancellationToken cancellationToken)
    {
        if (packageStream is null || !packageStream.CanRead) throw new ValidationException("同步包不可读");
        await using var memory = new MemoryStream();
        await packageStream.CopyToAsync(memory, cancellationToken);
        if (memory.Length <= 0 || memory.Length > MaxPackageBytes) throw new ValidationException("同步包大小无效", ErrorCodes.SchemaOrPayloadTooLarge);
        var bytes = memory.ToArray();
        ZipArchive archive;
        try { archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read); }
        catch (InvalidDataException exception) { throw new ValidationException($"同步包压缩结构无效: {exception.Message}"); }
        using (archive)
        {
            ValidatePaths(archive);
            var manifest = await ReadJsonAsync<SyncManifest>(archive, "manifest.json", cancellationToken);
            if (!string.Equals(manifest.Magic, Magic, StringComparison.Ordinal) || !string.Equals(manifest.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
                throw new ValidationException("同步包格式或版本不兼容");
            if (!string.Equals(manifest.TenantId, Tenant(), StringComparison.Ordinal) || !string.Equals(manifest.AppCode, App(), StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("同步包工作区与当前工作区不匹配", ErrorCodes.PermissionDenied);
            var data = await ReadBytesAsync(archive, manifest.DataEntry, cancellationToken);
            if (!string.Equals(Sha256(data), manifest.DataSha256, StringComparison.OrdinalIgnoreCase)) throw new ValidationException("同步包校验和不匹配");
            return (manifest, JsonSerializer.Deserialize<SyncSnapshot>(data, JsonOptions) ?? throw new ValidationException("同步包数据为空"), bytes);
        }
    }

    private static void ValidatePaths(ZipArchive archive)
    {
        if (archive.Entries.Count > 10000) throw new ValidationException("同步包条目过多");
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            if (path.StartsWith('/') || path.Contains("../", StringComparison.Ordinal) || path.Contains("..\\", StringComparison.Ordinal))
                throw new ValidationException("同步包包含非法路径");
        }
    }

    private static async Task<T> ReadJsonAsync<T>(ZipArchive archive, string name, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<T>(await ReadBytesAsync(archive, name, cancellationToken), JsonOptions)
            ?? throw new ValidationException($"同步包缺少 {name}");

    private static async Task<byte[]> ReadBytesAsync(ZipArchive archive, string name, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(name) ?? throw new ValidationException($"同步包缺少 {name}");
        await using var stream = entry.Open();
        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] content, CancellationToken cancellationToken)
    {
        await using var stream = archive.CreateEntry(name, CompressionLevel.Fastest).Open();
        await stream.WriteAsync(content, cancellationToken);
    }

    private static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content));
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class SyncManifest
    {
        public string Magic { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string AppCode { get; set; } = string.Empty;
        public DateTime ExportedAt { get; set; }
        public string? SourceDeviceId { get; set; }
        public string? ProjectId { get; set; }
        public bool IncludeAttachments { get; set; }
        public string DataEntry { get; set; } = string.Empty;
        public string DataSha256 { get; set; } = string.Empty;
        public int ProjectCount { get; set; }
        public int MemberCount { get; set; }
        public int MilestoneCount { get; set; }
        public int TaskCount { get; set; }
        public int DependencyCount { get; set; }
        public int AttachmentCount { get; set; }
        public long JournalSequenceNo { get; set; }
    }

    private sealed class SyncSnapshot
    {
        public List<ProjectManagementProjectEntity> Projects { get; set; } = [];
        public List<ProjectManagementProjectMemberEntity> Members { get; set; } = [];
        public List<ProjectManagementMilestoneEntity> Milestones { get; set; } = [];
        public List<ProjectManagementTaskEntity> Tasks { get; set; } = [];
        public List<ProjectManagementTaskDependencyEntity> Dependencies { get; set; } = [];
        public List<ProjectManagementLabelEntity> Labels { get; set; } = [];
        public List<ProjectManagementTaskLabelEntity> TaskLabels { get; set; } = [];
        public List<ProjectManagementTaskParticipantEntity> Participants { get; set; } = [];
        public List<ProjectManagementTaskTimeLogEntity> TimeLogs { get; set; } = [];
        public List<ProjectManagementTaskTemplateEntity> Templates { get; set; } = [];
        public List<ProjectManagementTaskOccurrenceEntity> Occurrences { get; set; } = [];
        public List<ProjectManagementActivityEntity> Activities { get; set; } = [];
        public List<ProjectManagementTaskCommentEntity> Comments { get; set; } = [];
        public List<ProjectManagementTaskAttachmentEntity> Attachments { get; set; } = [];
    }
}
