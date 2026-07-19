using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
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
    private const string SchemaVersion = "3";
    private const int MaxPackageBytes = 200 * 1024 * 1024;
    private const long MaxEntryUncompressedBytes = 200L * 1024 * 1024;
    private const long MaxArchiveUncompressedBytes = 400L * 1024 * 1024;
    private const long MaxManifestBytes = 1L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim DeviceWriteLock = new(1, 1);

    public async Task<(byte[] Content, string FileName)> ExportAsync(ProjectManagementSyncExportRequest request, CancellationToken cancellationToken = default)
    {
        var mode = NormalizeMode(request.Mode);
        var projectIds = await ResolveScopeAsync(request.ProjectId, cancellationToken);
        var sinceSequenceNo = await ResolveSinceSequenceNoAsync(request, mode, cancellationToken);
        var journals = await LoadJournalItemsAsync(projectIds, mode == "Incremental" ? sinceSequenceNo : 0, cancellationToken);
        var snapshot = mode == "Incremental"
            ? LoadIncrementalSnapshot(journals)
            : mode == "History" ? new SyncSnapshot() : await LoadSnapshotAsync(projectIds, cancellationToken);
        var journalSequenceNo = await GetJournalSequenceNoAsync(projectIds, cancellationToken);
        var packageId = Guid.NewGuid().ToString("N");
        var data = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        var journalData = JsonSerializer.SerializeToUtf8Bytes(journals, JsonOptions);
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
            Mode = mode,
            SinceSequenceNo = sinceSequenceNo,
            DataEntry = "data.json",
            DataSha256 = Sha256(data),
            JournalEntry = "journal.json",
            JournalSha256 = Sha256(journalData),
            JournalCount = journals.Count,
            ProjectCount = snapshot.Projects.Count,
            MemberCount = snapshot.Members.Count,
            MilestoneCount = snapshot.Milestones.Count,
            TaskCount = snapshot.Tasks.Count,
            DependencyCount = snapshot.Dependencies.Count,
            AttachmentCount = snapshot.Attachments.Count,
            JournalSequenceNo = journalSequenceNo
        };

        var attachmentChecksums = new Dictionary<string, string>(StringComparer.Ordinal);
        var attachmentEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        var attachmentPaths = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (request.IncludeAttachments)
        {
            if (fileStore is null) throw new ValidationException("文件服务不可用，无法导出附件");
            foreach (var attachment in snapshot.Attachments)
            {
                await using var sourceStream = await fileStore.OpenReadAsync(attachment.FileId, cancellationToken);
                await using var attachmentBytes = new MemoryStream();
                await sourceStream.CopyToAsync(attachmentBytes, cancellationToken);
                if (attachmentBytes.Length > MaxPackageBytes) throw new ValidationException("单个附件超过同步包限制", ErrorCodes.SchemaOrPayloadTooLarge);
                var bytes = attachmentBytes.ToArray();
                var checksum = Sha256(bytes).ToLowerInvariant();
                var entryName = $"attachments/{checksum}";
                attachmentChecksums[attachment.FileId] = checksum;
                attachmentEntries[attachment.FileId] = entryName;
                attachmentPaths.TryAdd(entryName, bytes);
            }
        }

        manifest.AttachmentSha256 = attachmentChecksums;
        manifest.AttachmentEntries = attachmentEntries;
        manifest.SignatureAlgorithm = "HMAC-SHA256";
        manifest.SignatureKeyId = "workspace-derived-v1";
        manifest.Signature = ComputeManifestSignature(manifest);
        await using var finalizedOutput = new MemoryStream();
        using (var finalizedArchive = new ZipArchive(finalizedOutput, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntryAsync(finalizedArchive, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions), cancellationToken);
            await WriteEntryAsync(finalizedArchive, "data.json", data, cancellationToken);
            await WriteEntryAsync(finalizedArchive, "journal.json", journalData, cancellationToken);
            if (request.IncludeAttachments)
            {
                if (fileStore is null) throw new ValidationException("文件服务不可用，无法导出附件");
                foreach (var attachment in attachmentPaths)
                {
                    await WriteEntryAsync(finalizedArchive, attachment.Key, attachment.Value, cancellationToken);
                }
            }
        }

        var content = finalizedOutput.ToArray();
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
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.SequenceNo > sinceSequenceNo && (item.ProjectId == null || projectIds.Contains(item.ProjectId)) && !item.IsDeleted)
            .OrderBy(item => item.SequenceNo, OrderByType.Asc)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(cancellationToken);
        return rows.Select(item =>
        {
            var metadata = ProjectManagementSyncJournalWriter.ReadMetadata(item.PayloadJson);
            return new ProjectManagementSyncJournalItem(
                item.SequenceNo, item.AggregateType, item.AggregateId, item.ProjectId, item.Operation,
                item.VersionNo, item.PayloadJson, item.TraceId, item.CreatedTime, metadata.Source,
                metadata.FieldChanges, item.DeviceId);
        }).ToList();
    }

    public async Task<ProjectManagementSyncWatermarkResponse> AcknowledgeAsync(ProjectManagementSyncAcknowledgeRequest request, CancellationToken cancellationToken = default)
    {
        var deviceId = NormalizeRequiredDevice(request.DeviceId);
        if (request.SequenceNo < 0) throw new ValidationException("同步水位不能为负数");
        var current = await GetJournalSequenceNoAsync(await ResolveScopeAsync(null, cancellationToken), cancellationToken);
        if (request.SequenceNo > current) throw new ValidationException("不能确认尚未存在的同步水位");
        var db = databaseAccessor.GetCurrentDb();
        var row = await AdvanceAcknowledgedWatermarkAsync(db, deviceId, request.SequenceNo, cancellationToken);
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
        var uncompressedSize = ValidateArchiveResources(archive);
        ValidatePaths(archive);
        var manifest = await ReadJsonAsync<SyncManifest>(archive, "manifest.json", MaxManifestBytes, cancellationToken);
        if (!string.Equals(manifest.Magic, Magic, StringComparison.Ordinal) || !string.Equals(manifest.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
            throw new ValidationException("同步包格式或版本不兼容");
        if (!string.Equals(manifest.TenantId, Tenant(), StringComparison.Ordinal) || !string.Equals(manifest.AppCode, App(), StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("同步包工作区与当前工作区不匹配", ErrorCodes.PermissionDenied);

        ValidateManifestEntries(manifest, archive);
        var signatureValid = VerifyManifestSignature(manifest);
        var data = await ReadBytesAsync(archive, manifest.DataEntry, MaxEntryUncompressedBytes, cancellationToken);
        if (!string.Equals(Sha256(data), manifest.DataSha256, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("同步包校验和不匹配");
        var journalData = await ReadBytesAsync(archive, manifest.JournalEntry, MaxEntryUncompressedBytes, cancellationToken);
        if (!string.Equals(Sha256(journalData), manifest.JournalSha256, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("同步包 Journal 校验和不匹配");
        var snapshot = JsonSerializer.Deserialize<SyncSnapshot>(data, JsonOptions) ?? throw new ValidationException("同步包数据为空");
        var journalRecords = JsonSerializer.Deserialize<List<ProjectManagementSyncJournalItem>>(journalData, JsonOptions) ?? [];
        if (journalRecords.Count != manifest.JournalCount) throw new ValidationException("同步包 Journal 数量与 Manifest 不一致");
        var projectIds = await ResolveScopeAsync(manifest.ProjectId, cancellationToken);
        var conflicts = await DetectConflictsAsync(snapshot, projectIds, cancellationToken);
        var replay = await FindImportResultAsync(manifest.PackageId, null, cancellationToken);
        var warnings = manifest.IncludeAttachments && snapshot.Attachments.Count > 0
            ? new[] { "附件内容将在导入阶段按 FileId 和校验和校验" }
            : manifest.Mode == "Incremental" && manifest.JournalCount == 0
                ? new[] { "起始水位后没有变更，已生成空同步包" }
                : Array.Empty<string>();
        return new ProjectManagementSyncPreviewResponse(
            manifest.PackageId, manifest.SchemaVersion, manifest.TenantId, manifest.AppCode,
            manifest.ExportedAt, manifest.SourceDeviceId, snapshot.Projects.Count, snapshot.Members.Count,
            snapshot.Milestones.Count, snapshot.Tasks.Count, snapshot.Dependencies.Count, snapshot.Attachments.Count,
            packageBytes.LongLength, Sha256(packageBytes), manifest.JournalSequenceNo, signatureValid, warnings,
            conflicts.Select(FormatConflict).ToList(), manifest.Mode, manifest.SinceSequenceNo, conflicts, replay is not null,
            manifest.SignatureAlgorithm, manifest.SignatureKeyId, signatureValid, manifest.JournalCount,
            manifest.JournalCount > 0 || manifest.Mode == "Full", manifest.AttachmentEntries.Count,
            signatureValid ? "Valid" : "InvalidSignature", uncompressedSize, archive.Entries.Count, true);
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
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey, manifest.PackageId);
        var replay = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? null
            : await FindImportResultAsync(manifest.PackageId, idempotencyKey, cancellationToken);
        if (replay is not null) return replay with { Replayed = true };
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
        var importId = Guid.NewGuid().ToString("N");
        var traceId = global::System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        var transactionStarted = false;
        try
        {
            if (operationId is not null && operationWriter is not null)
            {
                await operationWriter.StartAsync(operationId, "sync.import", $"{{\"packageId\":\"{manifest.PackageId}\",\"strategy\":\"{strategy}\"}}", operationId, cancellationToken);
                operationStarted = true;
            }
            db.Ado.BeginTran();
            transactionStarted = true;
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
            var result = new ProjectManagementSyncImportResponse(
                manifest.PackageId, strategy, inserted, updated, skipped, attachmentCount, warnings,
                importId, traceId, false, conflicts.Count, conflicts);
            await db.Insertable(new ProjectManagementOperationEntity
            {
                Id = importId,
                TenantId = Tenant(),
                AppCode = App(),
                OperationType = "sync.import",
                Status = "Succeeded",
                Phase = "Completed",
                ProgressPercent = 100,
                VersionNo = 1,
                ImpactJson = JsonSerializer.Serialize(new SyncImportImpact(manifest.PackageId, idempotencyKey, result), JsonOptions),
                TraceId = traceId,
                ActorUserId = UserId(),
                StartedTime = DateTime.UtcNow,
                CompletedTime = DateTime.UtcNow,
                CreatedBy = UserId(),
                CreatedTime = DateTime.UtcNow
            }).ExecuteCommandAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.DeviceId))
                await AdvanceAcknowledgedWatermarkAsync(db, NormalizeRequiredDevice(request.DeviceId), manifest.JournalSequenceNo, cancellationToken);
            db.Ado.CommitTran();
            transactionStarted = false;
            if (operationId is not null && operationWriter is not null) await operationWriter.SucceedAsync(operationId, cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            if (transactionStarted)
            {
                db.Ado.RollbackTran();
                transactionStarted = false;
            }
            foreach (var fileId in importedFiles)
            {
                try { if (fileStore is not null) await fileStore.DeleteAsync(fileId, cancellationToken); } catch { }
            }
            if (operationStarted && operationId is not null && operationWriter is not null)
            {
                try { await operationWriter.FailAsync(operationId, exception.Message, CancellationToken.None); } catch { }
            }
            try
            {
                await PersistImportFailureAsync(new ProjectManagementSyncImportResponse(
                    manifest.PackageId, strategy, inserted, updated, skipped, 0, warnings,
                    importId, traceId, false, conflicts.Count, conflicts), idempotencyKey, exception.Message, CancellationToken.None);
            }
            catch { }
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

    private async Task<IReadOnlyList<ProjectManagementSyncJournalItem>> LoadJournalItemsAsync(
        IReadOnlyList<string> projectIds,
        long sinceSequenceNo,
        CancellationToken cancellationToken)
    {
        var journals = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementSyncJournalEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.SequenceNo > sinceSequenceNo && (item.ProjectId == null || projectIds.Contains(item.ProjectId)) && !item.IsDeleted)
            .OrderBy(item => item.SequenceNo, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return journals.Select(item =>
        {
            var metadata = ProjectManagementSyncJournalWriter.ReadMetadata(item.PayloadJson);
            return new ProjectManagementSyncJournalItem(
                item.SequenceNo, item.AggregateType, item.AggregateId, item.ProjectId, item.Operation,
                item.VersionNo, item.PayloadJson, item.TraceId, item.CreatedTime, metadata.Source,
                metadata.FieldChanges, item.DeviceId);
        }).ToList();
    }

    private static SyncSnapshot LoadIncrementalSnapshot(IReadOnlyList<ProjectManagementSyncJournalItem> journals)
    {
        var snapshot = new SyncSnapshot();
        foreach (var journal in journals)
        {
            if (string.IsNullOrWhiteSpace(journal.PayloadJson)) continue;
            switch (journal.AggregateType)
            {
                case "Project": AddUnique(snapshot.Projects, DeserializePayload<ProjectManagementProjectEntity>(journal.PayloadJson)); break;
                case "ProjectMember": AddUnique(snapshot.Members, DeserializePayload<ProjectManagementProjectMemberEntity>(journal.PayloadJson)); break;
                case "Milestone": AddUnique(snapshot.Milestones, DeserializePayload<ProjectManagementMilestoneEntity>(journal.PayloadJson)); break;
                case "Task": AddUnique(snapshot.Tasks, DeserializePayload<ProjectManagementTaskEntity>(journal.PayloadJson)); break;
                case "TaskDependency": AddUnique(snapshot.Dependencies, DeserializePayload<ProjectManagementTaskDependencyEntity>(journal.PayloadJson)); break;
                case "Label": AddUnique(snapshot.Labels, DeserializePayload<ProjectManagementLabelEntity>(journal.PayloadJson)); break;
                case "TaskLabel": AddUnique(snapshot.TaskLabels, DeserializePayload<ProjectManagementTaskLabelEntity>(journal.PayloadJson)); break;
                case "Participant": AddUnique(snapshot.Participants, DeserializePayload<ProjectManagementTaskParticipantEntity>(journal.PayloadJson)); break;
                case "TimeLog": AddUnique(snapshot.TimeLogs, DeserializePayload<ProjectManagementTaskTimeLogEntity>(journal.PayloadJson)); break;
                case "TaskTemplate": AddUnique(snapshot.Templates, DeserializePayload<ProjectManagementTaskTemplateEntity>(journal.PayloadJson)); break;
                case "TaskOccurrence": AddUnique(snapshot.Occurrences, DeserializePayload<ProjectManagementTaskOccurrenceEntity>(journal.PayloadJson)); break;
                case "Activity": AddUnique(snapshot.Activities, DeserializePayload<ProjectManagementActivityEntity>(journal.PayloadJson)); break;
                case "TaskComment": AddUnique(snapshot.Comments, DeserializePayload<ProjectManagementTaskCommentEntity>(journal.PayloadJson)); break;
                case "TaskAttachment": AddUnique(snapshot.Attachments, DeserializePayload<ProjectManagementTaskAttachmentEntity>(journal.PayloadJson)); break;
            }
        }
        return snapshot;
    }

    private async Task<long> ResolveSinceSequenceNoAsync(ProjectManagementSyncExportRequest request, string mode, CancellationToken cancellationToken)
    {
        if (mode is "Full" or "History") return 0;
        if (request.SinceSequenceNo < 0) throw new ValidationException("增量同步起始水位不能为负数");
        if (request.SinceSequenceNo > 0) return request.SinceSequenceNo;
        if (string.IsNullOrWhiteSpace(request.DeviceId)) throw new ValidationException("增量导出必须提供设备标识或起始水位");
        var deviceId = NormalizeRequiredDevice(request.DeviceId);
        var row = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementSyncDeviceEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.DeviceId == deviceId && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        return row?.LastAcknowledgedSequenceNo ?? 0;
    }

    private static void AddUnique<T>(ICollection<T> target, T value) where T : EntityBase
    {
        if (!target.Any(item => item.Id == value.Id)) target.Add(value);
    }

    private static T DeserializePayload<T>(string payload) where T : EntityBase, new() =>
        JsonSerializer.Deserialize<T>(payload, JsonOptions) ?? throw new ValidationException("同步日志载荷无效");

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
        await ProjectManagementMutationTransaction.RunAsync(db, () => TouchDeviceCoreAsync(db, normalized, sequenceNo, cancellationToken));
    }

    private async Task TouchDeviceCoreAsync(ISqlSugarClient db, string normalized, long sequenceNo, CancellationToken cancellationToken)
    {
        await DeviceWriteLock.WaitAsync(cancellationToken);
        try
        {
            var rows = await db.Queryable<ProjectManagementSyncDeviceEntity>().Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.DeviceId == normalized && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var row = rows.FirstOrDefault() ?? new ProjectManagementSyncDeviceEntity { TenantId = Tenant(), AppCode = App(), DeviceId = normalized, CreatedBy = UserId(), CreatedTime = now };
            row.LastExportedSequenceNo = Math.Max(row.LastExportedSequenceNo, sequenceNo);
            row.LastSeenAt = now;
            row.UpdatedBy = UserId();
            row.UpdatedTime = now;
            if (rows.Count == 0) await db.Insertable(row).ExecuteCommandAsync(cancellationToken); else await db.Updateable(row).ExecuteCommandAsync(cancellationToken);
        }
        finally
        {
            DeviceWriteLock.Release();
        }
    }

    private async Task<ProjectManagementSyncDeviceEntity> AdvanceAcknowledgedWatermarkAsync(ISqlSugarClient db, string deviceId, long sequenceNo, CancellationToken cancellationToken)
    {
        await DeviceWriteLock.WaitAsync(cancellationToken);
        try
        {
            var rows = await db.Queryable<ProjectManagementSyncDeviceEntity>()
                .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.DeviceId == deviceId && !item.IsDeleted)
                .Take(1).ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var row = rows.FirstOrDefault() ?? new ProjectManagementSyncDeviceEntity
            {
                TenantId = Tenant(), AppCode = App(), DeviceId = deviceId,
                CreatedBy = UserId(), CreatedTime = now
            };
            row.LastAcknowledgedSequenceNo = Math.Max(row.LastAcknowledgedSequenceNo, sequenceNo);
            row.LastSeenAt = now;
            row.UpdatedBy = UserId();
            row.UpdatedTime = now;
            if (rows.Count == 0) await db.Insertable(row).ExecuteCommandAsync(cancellationToken);
            else await db.Updateable(row).ExecuteCommandAsync(cancellationToken);
            return row;
        }
        finally
        {
            DeviceWriteLock.Release();
        }
    }

    private static string NormalizeRequiredDevice(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException("设备标识不能为空");
        var normalized = value.Trim();
        if (normalized.Length > 120 || normalized.Any(char.IsControl)) throw new ValidationException("设备标识长度或字符无效");
        return normalized;
    }
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);

    private async Task<IReadOnlyList<ProjectManagementSyncConflict>> DetectConflictsAsync(SyncSnapshot snapshot, IReadOnlyList<string> projectIds, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var conflicts = new List<ProjectManagementSyncConflict>();
        var projectIdsInPackage = snapshot.Projects.Select(item => item.Id).ToList();
        var existingProjects = await db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => projectIdsInPackage.Contains(item.Id) && projectIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        conflicts.AddRange(existingProjects.Select(local =>
        {
            var remote = snapshot.Projects.First(item => item.Id == local.Id);
            return new ProjectManagementSyncConflict(
                "Project", local.Id, local.Id, "*",
                JsonSerializer.Serialize(local, JsonOptions),
                JsonSerializer.Serialize(remote, JsonOptions),
                local.VersionNo, remote.VersionNo, "Skip");
        }));
        var taskIds = snapshot.Tasks.Select(item => item.Id).ToList();
        var existingTasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => taskIds.Contains(item.Id) && projectIds.Contains(item.ProjectId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        conflicts.AddRange(existingTasks.Select(local =>
        {
            var remote = snapshot.Tasks.First(item => item.Id == local.Id);
            return new ProjectManagementSyncConflict(
                "Task", local.Id, local.ProjectId, "*",
                JsonSerializer.Serialize(local, JsonOptions),
                JsonSerializer.Serialize(remote, JsonOptions),
                local.VersionNo, remote.VersionNo, "Skip");
        }));
        return conflicts;
    }

    private static string FormatConflict(ProjectManagementSyncConflict conflict) =>
        $"{conflict.AggregateType}:{conflict.AggregateId}:local-version-{conflict.LocalVersionNo}:remote-version-{conflict.RemoteVersionNo}";

    private async Task<ProjectManagementSyncImportResponse?> FindImportResultAsync(string packageId, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.OperationType == "sync.import" && item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == UserId() && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(200)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            var impact = JsonSerializer.Deserialize<SyncImportImpact>(row.ImpactJson, JsonOptions);
            if (impact is null || !string.Equals(impact.PackageId, packageId, StringComparison.Ordinal)) continue;
            if (idempotencyKey is not null && !string.Equals(impact.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)) continue;
            if (!string.Equals(row.Status, "Succeeded", StringComparison.Ordinal)) continue;
            return impact.Result;
        }
        return null;
    }

    private async Task PersistImportFailureAsync(ProjectManagementSyncImportResponse result, string idempotencyKey, string errorMessage, CancellationToken cancellationToken)
    {
        var replay = await FindImportResultAsync(result.PackageId, idempotencyKey, cancellationToken);
        if (replay is not null) return;
        var now = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Insertable(new ProjectManagementOperationEntity
        {
            Id = result.ImportId,
            TenantId = Tenant(),
            AppCode = App(),
            OperationType = "sync.import",
            Status = "Failed",
            Phase = "Failed",
            ProgressPercent = 0,
            VersionNo = 1,
            ImpactJson = JsonSerializer.Serialize(new SyncImportImpact(result.PackageId, idempotencyKey, result), JsonOptions),
            ErrorMessage = errorMessage,
            TraceId = result.TraceId,
            ActorUserId = UserId(),
            StartedTime = now,
            CompletedTime = now,
            CreatedBy = UserId(),
            CreatedTime = now
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static string NormalizeIdempotencyKey(string? value, string packageId)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? packageId : value.Trim();
        if (normalized.Length > 128) throw new ValidationException("幂等键长度不能超过 128 个字符");
        return normalized;
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
        var referencedProjectIds = snapshot.Members.Select(item => item.ProjectId)
            .Concat(snapshot.Milestones.Select(item => item.ProjectId))
            .Concat(snapshot.Tasks.Select(item => item.ProjectId))
            .Concat(snapshot.Dependencies.Select(item => item.ProjectId))
            .Concat(snapshot.TaskLabels.Select(item => item.ProjectId))
            .Concat(snapshot.Participants.Select(item => item.ProjectId))
            .Concat(snapshot.TimeLogs.Select(item => item.ProjectId))
            .Concat(snapshot.Occurrences.Select(item => item.ProjectId))
            .Concat(snapshot.Activities.Select(item => item.ProjectId))
            .Concat(snapshot.Comments.Select(item => item.ProjectId))
            .Concat(snapshot.Attachments.Select(item => item.ProjectId));
        if (referencedProjectIds.Any(item => !projectIds.Contains(item, StringComparer.Ordinal)))
            throw new ValidationException("同步包关联数据超出项目授权范围", ErrorCodes.PermissionDenied);
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
            var entryName = manifest.AttachmentEntries.GetValueOrDefault(source.FileId)
                ?? throw new ValidationException($"同步包缺少附件 {source.FileId}");
            var entry = archive.GetEntry(entryName) ?? throw new ValidationException($"同步包缺少附件 {source.FileId}");
            var bytes = await ReadBytesAsync(archive, entryName, MaxEntryUncompressedBytes, cancellationToken);
            if (!manifest.AttachmentSha256.TryGetValue(source.FileId, out var expectedChecksum) ||
                !string.Equals(Sha256(bytes), expectedChecksum, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException($"附件 {source.FileId} 校验和不匹配");
            await using var uploadStream = new MemoryStream(bytes);
            var formFile = new FormFile(uploadStream, 0, bytes.Length, "file", source.FileName)
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
            ValidateArchiveResources(archive);
            ValidatePaths(archive);
            var manifest = await ReadJsonAsync<SyncManifest>(archive, "manifest.json", MaxManifestBytes, cancellationToken);
            if (!string.Equals(manifest.Magic, Magic, StringComparison.Ordinal) || !string.Equals(manifest.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
                throw new ValidationException("同步包格式或版本不兼容");
            manifest.Mode = NormalizeMode(manifest.Mode);
            if (manifest.SinceSequenceNo < 0) throw new ValidationException("同步包起始水位不能为负数");
            if (!string.Equals(manifest.TenantId, Tenant(), StringComparison.Ordinal) || !string.Equals(manifest.AppCode, App(), StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("同步包工作区与当前工作区不匹配", ErrorCodes.PermissionDenied);
            ValidateManifestEntries(manifest, archive);
            if (!VerifyManifestSignature(manifest)) throw new ValidationException("同步包签名校验失败");
            var data = await ReadBytesAsync(archive, manifest.DataEntry, MaxEntryUncompressedBytes, cancellationToken);
            if (!string.Equals(Sha256(data), manifest.DataSha256, StringComparison.OrdinalIgnoreCase)) throw new ValidationException("同步包校验和不匹配");
            var journalData = await ReadBytesAsync(archive, manifest.JournalEntry, MaxEntryUncompressedBytes, cancellationToken);
            if (!string.Equals(Sha256(journalData), manifest.JournalSha256, StringComparison.OrdinalIgnoreCase)) throw new ValidationException("同步包 Journal 校验和不匹配");
            var snapshot = JsonSerializer.Deserialize<SyncSnapshot>(data, JsonOptions) ?? throw new ValidationException("同步包数据为空");
            var journalRecords = JsonSerializer.Deserialize<List<ProjectManagementSyncJournalItem>>(journalData, JsonOptions) ?? [];
            if (journalRecords.Count != manifest.JournalCount) throw new ValidationException("同步包 Journal 数量与 Manifest 不一致");
            return (manifest, snapshot, bytes);
        }
    }

    private static long ValidateArchiveResources(ZipArchive archive)
    {
        if (archive.Entries.Count > 10000) throw new ValidationException("同步包条目过多");
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length < 0 || entry.Length > MaxEntryUncompressedBytes)
                throw new ValidationException("同步包单项解压大小超过限制", ErrorCodes.SchemaOrPayloadTooLarge);
            total = checked(total + entry.Length);
            if (total > MaxArchiveUncompressedBytes)
                throw new ValidationException("同步包解压总大小超过限制", ErrorCodes.SchemaOrPayloadTooLarge);
        }
        return total;
    }

    private static void ValidatePaths(ZipArchive archive)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            if (!paths.Add(path) || path.StartsWith('/') || path.Contains(":", StringComparison.Ordinal) || path.Contains("../", StringComparison.Ordinal) || path.Contains("..\\", StringComparison.Ordinal) || path.Split('/').Any(string.IsNullOrWhiteSpace))
                throw new ValidationException("同步包包含非法路径");
        }
    }

    private static void ValidateManifestEntries(SyncManifest manifest, ZipArchive archive)
    {
        if (!string.Equals(manifest.DataEntry, "data.json", StringComparison.Ordinal) || !string.Equals(manifest.JournalEntry, "journal.json", StringComparison.Ordinal))
            throw new ValidationException("同步包 Manifest 数据入口无效");
        if (manifest.AttachmentCount < 0 || manifest.JournalCount < 0 || manifest.AttachmentSha256 is null || manifest.AttachmentEntries is null || manifest.AttachmentSha256.Count != manifest.AttachmentEntries.Count || manifest.AttachmentCount != manifest.AttachmentSha256.Count)
            throw new ValidationException("同步包附件校验和与归档入口数量不一致");
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapping in manifest.AttachmentEntries)
        {
            var entry = mapping.Value.Replace('\\', '/');
            var checksum = manifest.AttachmentSha256.GetValueOrDefault(mapping.Key);
            if (!entry.StartsWith("attachments/", StringComparison.Ordinal) || entry.Length != "attachments/".Length + 64 || !entry.Skip("attachments/".Length).All(Uri.IsHexDigit) || !string.Equals(checksum, entry["attachments/".Length..], StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("同步包附件入口或校验和无效");
            if (!paths.Add(entry) || archive.GetEntry(entry) is null) throw new ValidationException("同步包附件入口重复或缺失");
        }
        var expected = new HashSet<string>(StringComparer.Ordinal) { "manifest.json", "data.json", "journal.json" };
        expected.UnionWith(paths);
        if (archive.Entries.Any(item => !expected.Contains(item.FullName.Replace('\\', '/'))))
            throw new ValidationException("同步包包含 Manifest 未声明的归档入口");
    }

    private static async Task<T> ReadJsonAsync<T>(ZipArchive archive, string name, long maxBytes, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<T>(await ReadBytesAsync(archive, name, maxBytes, cancellationToken), JsonOptions)
            ?? throw new ValidationException($"同步包缺少 {name}");

    private static async Task<byte[]> ReadBytesAsync(ZipArchive archive, string name, long maxBytes, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(name) ?? throw new ValidationException($"同步包缺少 {name}");
        await using var stream = entry.Open();
        await using var memory = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (memory.Length + read > maxBytes) throw new ValidationException($"同步包入口 {name} 解压大小超过限制", ErrorCodes.SchemaOrPayloadTooLarge);
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return memory.ToArray();
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] content, CancellationToken cancellationToken)
    {
        await using var stream = archive.CreateEntry(name, CompressionLevel.Fastest).Open();
        await stream.WriteAsync(content, cancellationToken);
    }

    private static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content));
    private string ComputeManifestSignature(SyncManifest manifest)
    {
        var original = manifest.Signature;
        manifest.Signature = string.Empty;
        var canonical = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        manifest.Signature = original;
        var key = Encoding.UTF8.GetBytes($"{Tenant()}:{App()}:project-management-bqsync-v1");
        return Convert.ToHexString(HMACSHA256.HashData(key, canonical));
    }

    private bool VerifyManifestSignature(SyncManifest manifest)
    {
        if (!string.Equals(manifest.SignatureAlgorithm, "HMAC-SHA256", StringComparison.Ordinal) ||
            !string.Equals(manifest.SignatureKeyId, "workspace-derived-v1", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.Signature)) return false;
        try
        {
            var expected = ComputeManifestSignature(manifest);
            var actualBytes = Convert.FromHexString(manifest.Signature);
            var expectedBytes = Convert.FromHexString(expected);
            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeMode(string? value) => value?.Trim() switch
    {
        null or "" or "Full" => "Full",
        "Incremental" => "Incremental",
        "History" => "History",
        _ => throw new ValidationException("同步导出模式必须是 Full、Incremental 或 History")
    };

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
        public string Mode { get; set; } = "Full";
        public long SinceSequenceNo { get; set; }
        public string DataEntry { get; set; } = string.Empty;
        public string DataSha256 { get; set; } = string.Empty;
        public string JournalEntry { get; set; } = string.Empty;
        public string JournalSha256 { get; set; } = string.Empty;
        public int JournalCount { get; set; }
        public string SignatureAlgorithm { get; set; } = string.Empty;
        public string SignatureKeyId { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public int ProjectCount { get; set; }
        public int MemberCount { get; set; }
        public int MilestoneCount { get; set; }
        public int TaskCount { get; set; }
        public int DependencyCount { get; set; }
        public int AttachmentCount { get; set; }
        public long JournalSequenceNo { get; set; }
        public Dictionary<string, string> AttachmentSha256 { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> AttachmentEntries { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed record SyncImportImpact(
        string PackageId,
        string IdempotencyKey,
        ProjectManagementSyncImportResponse Result);

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
