using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_data_space_exports")]
public sealed class ProjectManagementDataSpaceExportEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string PackageSha256 { get; set; } = string.Empty;
    public long PackageSize { get; set; }
    public string DatabaseSha256 { get; set; } = string.Empty;
    public string ManifestJson { get; set; } = "{}";
    public string EncryptionKeyCipherText { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime DownloadExpiresAt { get; set; }
    public int DownloadCount { get; set; }
    public int MaxDownloadCount { get; set; } = 3;
    [SugarColumn(IsNullable = true)] public DateTime? LastDownloadedAt { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? CompletedAt { get; set; }
}
