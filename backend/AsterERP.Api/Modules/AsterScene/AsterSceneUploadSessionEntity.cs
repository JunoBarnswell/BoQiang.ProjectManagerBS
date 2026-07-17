using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_upload_session")]
public sealed class AsterSceneUploadSessionEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string UploadCode { get; set; } = string.Empty;

    public string AssetType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ContentType { get; set; }

    public long SizeBytes { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Checksum { get; set; }

    public int TotalChunks { get; set; } = 1;

    public int UploadedChunks { get; set; }

    public string Status { get; set; } = "Pending";

    [SugarColumn(IsNullable = true)]
    public string? ClientMutationId { get; set; }

    public DateTime ExpiresAt { get; set; }
}
