using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Platform;

[SugarTable("system_application_publish_artifacts")]
public sealed class SystemApplicationPublishArtifactEntity : EntityBase
{
    public string TaskId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/zip";

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string StoredPath { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public DateTime? ExpiresAt { get; set; }
}
