using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_asset_version")]
public sealed class AsterSceneAssetVersionEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string AssetId { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public string VariantType { get; set; } = "original";

    public string Url { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ContentType { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? SizeBytes { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Checksum { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? MetadataJson { get; set; }

    public string Status { get; set; } = "Ready";
}
