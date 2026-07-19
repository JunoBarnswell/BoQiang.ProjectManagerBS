namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementDataSpaceExportRequest(
    string CurrentPassword,
    bool ConfirmRisk,
    string? Reason = null);

public sealed record ProjectManagementDataSpaceExportManifest(
    int FormatVersion,
    string TenantId,
    string AppCode,
    string DatabaseProvider,
    string SnapshotMode,
    int SchemaVersion,
    DateTime SnapshotAt,
    IReadOnlyList<string> SchemaObjects,
    string DatabaseSha256,
    string EncryptionAlgorithm);

public sealed record ProjectManagementDataSpaceExportResponse(
    string Id,
    string PackageName,
    string Status,
    string OperationId,
    long PackageSize,
    string PackageSha256,
    DateTime CreatedTime,
    DateTime? CompletedAt,
    DateTime DownloadExpiresAt,
    int DownloadCount,
    int MaxDownloadCount,
    ProjectManagementDataSpaceExportManifest? Manifest);

public sealed record ProjectManagementDataSpaceExportDownload(
    string FileName,
    string ContentType,
    Stream Stream);
