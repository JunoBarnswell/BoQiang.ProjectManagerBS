using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_public_work")]
public sealed class AsterScenePublicWorkEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string PublishVersionId { get; set; } = string.Empty;

    public string PublishCode { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Summary { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? CoverAssetId { get; set; }

    public string CreatorUserId { get; set; } = string.Empty;

    public string CreatorHandle { get; set; } = string.Empty;

    public string Visibility { get; set; } = "Public";

    public string Status { get; set; } = "Published";

    public int ViewCount { get; set; }

    public int LikeCount { get; set; }

    public int FavoriteCount { get; set; }

    public int RemixCount { get; set; }

    public int CommentCount { get; set; }

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? LastIndexedAt { get; set; }
}
