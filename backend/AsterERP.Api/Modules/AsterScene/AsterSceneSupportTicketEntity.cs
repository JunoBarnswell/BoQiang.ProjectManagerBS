using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_support_ticket")]
public sealed class AsterSceneSupportTicketEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "Open";

    public string Severity { get; set; } = "Normal";

    public string BundleJson { get; set; } = "{}";

    [SugarColumn(IsNullable = true)]
    public string? ClientMutationId { get; set; }
}
