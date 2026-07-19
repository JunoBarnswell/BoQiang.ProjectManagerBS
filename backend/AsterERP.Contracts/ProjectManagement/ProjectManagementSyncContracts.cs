namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementSyncExportRequest(
    string? ProjectId = null,
    bool IncludeAttachments = false,
    string? DeviceId = null,
    string Mode = "Full",
    long SinceSequenceNo = 0);

public sealed record ProjectManagementSyncConflict(
    string AggregateType,
    string AggregateId,
    string? ProjectId,
    string Field,
    string? LocalValue,
    string? RemoteValue,
    long? LocalVersionNo,
    long? RemoteVersionNo,
    string RecommendedStrategy,
    string? BaselineValue = null,
    bool BaselineKnown = false,
    bool LocalChanged = false,
    bool RemoteChanged = false);

public sealed record ProjectManagementSyncPreviewResponse(
    string PackageId,
    string SchemaVersion,
    string TenantId,
    string AppCode,
    DateTime ExportedAt,
    string? SourceDeviceId,
    int ProjectCount,
    int MemberCount,
    int MilestoneCount,
    int TaskCount,
    int DependencyCount,
    int AttachmentCount,
    long PackageSize,
    string PackageSha256,
    long JournalSequenceNo,
    bool IsCompatible,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Conflicts,
    string Mode = "Full",
    long SinceSequenceNo = 0,
    IReadOnlyList<ProjectManagementSyncConflict>? ConflictDetails = null,
    bool AlreadyImported = false,
    string SignatureAlgorithm = "",
    string SignatureKeyId = "",
    bool SignatureValid = false,
    int JournalCount = 0,
    bool HasChanges = true,
    int AttachmentEntryCount = 0,
    string ValidationState = "Valid",
    long UncompressedSize = 0,
    int ArchiveEntryCount = 0,
    bool PreviewOnly = true);

public sealed record ProjectManagementSyncImportRequest(
    string CurrentPassword,
    bool ConfirmRisk,
    string ConflictStrategy = "Skip",
    string? IdempotencyKey = null,
    string? DeviceId = null);

public sealed record ProjectManagementSyncImportResponse(
    string PackageId,
    string Strategy,
    int Inserted,
    int Updated,
    int Skipped,
    int AttachmentsImported,
    IReadOnlyList<string> Warnings,
    string ImportId = "",
    string TraceId = "",
    bool Replayed = false,
    int ConflictCount = 0,
    IReadOnlyList<ProjectManagementSyncConflict>? Conflicts = null);

public sealed record ProjectManagementSyncWatermarkResponse(
    string DeviceId,
    long CurrentSequenceNo,
    long AcknowledgedSequenceNo,
    DateTime? LastSeenAt);

public sealed record ProjectManagementSyncAcknowledgeRequest(
    string DeviceId,
    long SequenceNo);

public sealed record ProjectManagementSyncJournalItem(
    long SequenceNo,
    string AggregateType,
    string AggregateId,
    string? ProjectId,
    string Operation,
    long VersionNo,
    string PayloadJson,
    string TraceId,
    DateTime CreatedTime,
    string Source = "User",
    IReadOnlyList<ProjectManagementSyncFieldChange>? FieldChanges = null,
    string? DeviceId = null);

public sealed record ProjectManagementSyncFieldChange(
    string Field,
    string? Before,
    string? After);
