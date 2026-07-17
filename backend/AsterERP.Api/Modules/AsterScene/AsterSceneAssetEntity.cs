using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_asset")]
public sealed class AsterSceneAssetEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string AssetCode { get; set; } = string.Empty;

    public string AssetType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Status { get; set; } = "Ready";

    public int CurrentVersion { get; set; } = 1;

    [SugarColumn(IsNullable = true)]
    public string? SourceUrl { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RuntimeUrl { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ThumbnailUrl { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ContentType { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? SizeBytes { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Checksum { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? MetadataJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ClientMutationId { get; set; }
}
