using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_creator_profile")]
public sealed class AsterSceneCreatorProfileEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Handle { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Bio { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AvatarUrl { get; set; }

    public string Status { get; set; } = "Active";

    public int WorksCount { get; set; }

    public int FollowersCount { get; set; }

    public int FollowingCount { get; set; }
}
