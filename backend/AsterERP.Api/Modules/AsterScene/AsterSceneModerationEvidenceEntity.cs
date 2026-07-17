using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_moderation_evidence")]
public sealed class AsterSceneModerationEvidenceEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string CaseId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? AppealId { get; set; }

    public string SubmittedBy { get; set; } = string.Empty;

    public string EvidenceType { get; set; } = string.Empty;

    public string EvidenceJson { get; set; } = "{}";

    public string EvidenceHash { get; set; } = string.Empty;

    public string ClientMutationId { get; set; } = string.Empty;
}
