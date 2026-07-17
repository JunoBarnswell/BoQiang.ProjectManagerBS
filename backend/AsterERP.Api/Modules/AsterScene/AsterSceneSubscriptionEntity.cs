using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_subscription")]
public sealed class AsterSceneSubscriptionEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string PlanCode { get; set; } = "free";

    public string Status { get; set; } = "Active";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? EndsAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ClientMutationId { get; set; }
}
