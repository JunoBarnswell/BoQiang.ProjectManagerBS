using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_upload_chunk")]
public sealed class AsterSceneUploadChunkEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string UploadSessionId { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public int ChunkIndex { get; set; }

    public long SizeBytes { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Checksum { get; set; }

    public string StoragePath { get; set; } = string.Empty;
}
