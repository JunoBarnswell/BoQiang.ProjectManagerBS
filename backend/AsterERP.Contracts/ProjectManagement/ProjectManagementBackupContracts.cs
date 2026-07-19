namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementBackupRequest(string CurrentPassword, bool ConfirmRisk, string? Reason = null);

public sealed record ProjectManagementBackupDeleteRequest(string CurrentPassword, bool ConfirmRisk);

public sealed record ProjectManagementBackupDownload(string FileName, string ContentType, Stream Stream);

public sealed record ProjectManagementBackupResponse(
    string Id,
    string BackupName,
    string Sha256,
    long FileSize,
    string Status,
    string CreatedByUserId,
    DateTime CreatedTime,
    DateTime? CompletedAt,
    string? OperationId = null);

public sealed record ProjectManagementRestoreRequest(string CurrentPassword, bool ConfirmRisk);

public sealed record ProjectManagementDataSpaceImpact(
    string TenantId,
    string AppCode,
    int ProjectCount,
    int TaskCount,
    int MemberCount,
    int MilestoneCount,
    int AttachmentCount);

public sealed record ProjectManagementBackupRestorePreviewResponse(
    ProjectManagementBackupResponse Backup,
    ProjectManagementDataSpaceImpact CurrentDataSpace,
    ProjectManagementDataSpaceImpact BackupDataSpace,
    string ImpactScope,
    string FailureCompensationHint,
    string SuccessfulRestoreRollbackHint);
