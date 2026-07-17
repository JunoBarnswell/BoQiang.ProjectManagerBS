using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_appeal")]
public sealed class AsterSceneAppealEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string CaseId { get; set; } = string.Empty;

    public string AppellantUserId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = "Submitted";

    public string ClientMutationId { get; set; } = string.Empty;
}
