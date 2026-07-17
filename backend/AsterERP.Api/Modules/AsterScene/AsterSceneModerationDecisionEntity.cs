using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_moderation_decision")]
public sealed class AsterSceneModerationDecisionEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string CaseId { get; set; } = string.Empty;

    public string Decision { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Note { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;
}
