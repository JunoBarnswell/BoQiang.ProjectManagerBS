namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementBackupRequest(string CurrentPassword, bool ConfirmRisk, string? Reason = null);

public sealed record ProjectManagementBackupResponse(
    string Id,
    string BackupName,
    string Sha256,
    long FileSize,
    string Status,
    string CreatedByUserId,
    DateTime CreatedTime,
    DateTime? CompletedAt);

public sealed record ProjectManagementRestoreRequest(string CurrentPassword, bool ConfirmRisk);
