using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_community_reaction")]
public sealed class AsterSceneCommunityReactionEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string WorkId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string ReactionType { get; set; } = "Like";

    [SugarColumn(IsNullable = true)]
    public string? ClientMutationId { get; set; }
}
