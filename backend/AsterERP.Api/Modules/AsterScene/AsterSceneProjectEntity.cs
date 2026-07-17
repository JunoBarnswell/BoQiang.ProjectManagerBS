using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_project")]
public sealed class AsterSceneProjectEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    public string Visibility { get; set; } = "Private";

    public string Status { get; set; } = "Draft";

    public int CurrentRevision { get; set; } = 1;

    public string DocumentHash { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? CoverAssetId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? CurrentPublishCode { get; set; }

    public int PublishedVersion { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? CreateClientMutationId { get; set; }
}
